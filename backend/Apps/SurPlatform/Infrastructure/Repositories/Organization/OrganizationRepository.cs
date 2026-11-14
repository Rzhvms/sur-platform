using System.Data;
using Application.Constants;
using Application.Exceptions;
using Application.Ports.Repositories;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Repositories.Identity;
using Npgsql;
using Infrastructure.Repositories.Organization.Interfaces;
using Infrastructure.Repositories.Risks.Interfaces;

namespace Infrastructure.Repositories.Organization;

/// <inheritdoc/>
internal sealed partial class OrganizationRepository : IOrganizationRepository, IOrganizationIdentityRepository
{
    /// <summary>Открытое рабочее подключение текущего клиента</summary>
    private readonly IDbConnection connection;

    /// <summary>Обновление связей рисков при смене владельца процесса</summary>
    private readonly IRiskOrganizationRepository riskOrganization;

    /// <summary>Создаёт хранилище в контексте текущей клиентской базы</summary>
    /// <param name="connection">Подключение из клиентского scope</param>
    /// <param name="riskOrganization">Операции со связями рисков в транзакции процесса</param>
    public OrganizationRepository(IDbConnection connection, IRiskOrganizationRepository riskOrganization)
    {
        this.connection = connection;
        this.riskOrganization = riskOrganization;
    }

    /// <summary>Пустая необязательная часть SQL-выборки</summary>
    private const string EmptySqlFragment = "";

    /// <summary>Общий идентификатор оргзаписи</summary>
    private static readonly string Id = EntityMapper.ColName<DirectorateDal>(row => row.Id);

    /// <summary>Общая колонка состояния</summary>
    private static readonly string IsActive = EntityMapper.ColName<DirectorateDal>(row => row.IsActive);

    /// <summary>Общая версия оргзаписи</summary>
    private static readonly string Version = EntityMapper.ColName<DirectorateDal>(row => row.Version);

    /// <summary>Общий момент изменения</summary>
    private static readonly string UpdatedAt = EntityMapper.ColName<DirectorateDal>(row => row.UpdatedAt);

    /// <summary>Заполняет общие поля при обновлении оргзаписи</summary>
    private static readonly string Touch = $"{Version} = gen_random_uuid(), {UpdatedAt} = clock_timestamp()";

    /// <summary>Пустой поиск или нормализованный фрагмент отображаемого значения</summary>
    /// <param name="search">Поисковая строка</param>
    /// <returns>Строка поиска для параметра SQL</returns>
    private static string SearchTerm(string? search)
    {
        return search?.Trim() ?? string.Empty;
    }

    /// <summary>Исполняет команду под общей блокировкой Identity и оргструктуры</summary>
    /// <typeparam name="T">Результат команды</typeparam>
    /// <param name="actor">Администратор</param>
    /// <param name="sessionId">Его сессия</param>
    /// <param name="action">Тело транзакции</param>
    /// <returns>Результат после коммита</returns>
    /// <exception cref="IdentityCommandException">Нарушена уникальность оргструктуры</exception>
    private async Task<T> WriteAsync<T>(Guid actor, Guid sessionId, Func<IDbTransaction, Task<T>> action)
    {
        try
        {
            return await AdministrationTransaction.WriteAsync(connection, actor, sessionId, action);
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new IdentityCommandException(409, OrganizationErrors.Duplicate);
        }
    }

    /// <summary>Читает карточку из текущей базы</summary>
    /// <typeparam name="T">Тип таблицы оргструктуры</typeparam>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="transaction">Транзакция команды при чтении внутри записи</param>
    /// <returns>Карточка, включая неактивные записи</returns>
    /// <exception cref="IdentityCommandException">Запись отсутствует</exception>
    private async Task<T> GetAsync<T>(Guid id, IDbTransaction? transaction = null) where T : class, IOrganizationRow
    {
        var query = $"SELECT * FROM {EntityMapper.TbName<T>()} WHERE {Id} = {EntityMapper.ParameterName(nameof(id))}";
        return await connection.QuerySingleOrDefaultAsync<T>(query, new { id }, transaction)
            ?? throw new IdentityCommandException(404, OrganizationErrors.NotFound);
    }

    /// <summary>Сверяет версию записи с версией формы</summary>
    /// <param name="row">Текущее состояние</param>
    /// <param name="version">Ожидаемая версия</param>
    /// <exception cref="IdentityCommandException">Запись изменилась</exception>
    private static void RequireVersion(IOrganizationRow row, Guid version)
    {
        if (row.Version != version)
        {
            throw new IdentityCommandException(409, OrganizationErrors.StaleVersion);
        }
    }

    /// <summary>Проверяет, что дирекция доступна для новых связей</summary>
    /// <param name="id">Идентификатор дирекции</param>
    /// <param name="transaction">Текущая транзакция</param>
    /// <exception cref="IdentityCommandException">Дирекция недоступна</exception>
    private async Task RequireDirectorateAsync(Guid id, IDbTransaction transaction)
    {
        if (!(await GetAsync<DirectorateDal>(id, transaction)).IsActive)
        {
            throw new IdentityCommandException(409, OrganizationErrors.Ineligible);
        }
    }

    /// <summary>Читает страницу с постоянным порядком по идентификатору</summary>
    /// <typeparam name="T">Тип строк справочника</typeparam>
    /// <param name="after">Курсор</param>
    /// <param name="limit">Размер страницы</param>
    /// <param name="active">Состояние</param>
    /// <param name="search">Поисковый фрагмент</param>
    /// <param name="directorateId">Фильтр дирекции</param>
    /// <param name="searchExpression">Доверенное SQL-выражение для поиска</param>
    /// <param name="directorateColumn">Доверенная колонка дирекции, если предусмотрена</param>
    /// <param name="join">Доверенный JOIN для чтения связанных атрибутов</param>
    /// <param name="projection">Доверенные дополнительные поля</param>
    /// <param name="exactColumn">Доверенная колонка точного фильтра</param>
    /// <param name="exactValue">Необязательное значение точного фильтра</param>
    /// <returns>Строки страницы</returns>
    private async Task<T[]> ListAsync<T>(Guid after, int limit, bool? active, string? search,
        Guid? directorateId, string searchExpression, string? directorateColumn = null,
        string join = EmptySqlFragment, string projection = EmptySqlFragment,
        string? exactColumn = null, string? exactValue = null) where T : class, IOrganizationRow
    {
        var term = SearchTerm(search);
        var directorateFilter = directorateColumn is null
            ? string.Empty
            : $"AND (CAST({EntityMapper.ParameterName(nameof(directorateId))} AS uuid) IS NULL OR t.{directorateColumn} = {EntityMapper.ParameterName(nameof(directorateId))})";
        var exactFilter = exactColumn is null || exactValue is null
            ? string.Empty
            : $"AND t.{exactColumn} = {EntityMapper.ParameterName(nameof(exactValue))}";
        var query = $"""
            SELECT t.*{projection} FROM {EntityMapper.TbName<T>()} t {join}
            WHERE t.{Id} > {EntityMapper.ParameterName(nameof(after))}
              AND (CAST({EntityMapper.ParameterName(nameof(active))} AS boolean) IS NULL OR t.{IsActive} = {EntityMapper.ParameterName(nameof(active))})
              AND ({EntityMapper.ParameterName(nameof(term))} = '' OR strpos(lower({searchExpression}), lower({EntityMapper.ParameterName(nameof(term))})) > 0)
              {directorateFilter}
              {exactFilter}
            ORDER BY t.{Id} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var rows = await connection.QueryAsync<T>(query, new { after, limit, active, term, directorateId, exactValue });
        return rows.ToArray();
    }

    /// <summary>Сохраняет версию отображаемых атрибутов или состояния для отчётного среза</summary>
    /// <typeparam name="T">Тип оргзаписи</typeparam>
    /// <param name="row">Записанное состояние</param>
    /// <param name="actorId">Инициатор изменения</param>
    /// <param name="transaction">Транзакция записи</param>
    /// <returns>Сохранённое состояние</returns>
    private async Task<T> RecordAsync<T>(T row, Guid actorId, IDbTransaction transaction) where T : IOrganizationRow
    {
        var entityType = EntityMapper.TbName<T>(false);
        var entityId = row.Id;
        var version = row.Version;
        var snapshot = row is OrgPersonDal { FullName: null, Email: null }
            ? System.Text.Json.JsonSerializer.Serialize(await ReadPersonAsync(row.Id, transaction))
            : System.Text.Json.JsonSerializer.Serialize(row);
        var query = $"""
            INSERT INTO {EntityMapper.TbName<OrgHistoryDal>()}
                ({EntityMapper.ColName<OrgHistoryDal>(x => x.Id)}, {EntityMapper.ColName<OrgHistoryDal>(x => x.EntityId)},
                 {EntityMapper.ColName<OrgHistoryDal>(x => x.EntityType)}, {EntityMapper.ColName<OrgHistoryDal>(x => x.ActorId)},
                 {EntityMapper.ColName<OrgHistoryDal>(x => x.Snapshot)})
            VALUES ({EntityMapper.ParameterName(nameof(version))}, {EntityMapper.ParameterName(nameof(entityId))},
                {EntityMapper.ParameterName(nameof(entityType))}, {EntityMapper.ParameterName(nameof(actorId))},
                CAST({EntityMapper.ParameterName(nameof(snapshot))} AS jsonb))
            """;
        await connection.ExecuteAsync(query, new { version, entityId, entityType, actorId, snapshot }, transaction);
        return row;
    }

    /// <summary>Изменяет только состояние и сохраняет новую версию</summary>
    /// <typeparam name="T">Тип оргзаписи</typeparam>
    /// <param name="row">Проверенная запись</param>
    /// <param name="active">Требуемое состояние</param>
    /// <param name="actor">Инициатор</param>
    /// <param name="transaction">Текущая транзакция</param>
    /// <returns>Состояние после изменения</returns>
    private async Task<T> SetStateAsync<T>(T row, bool active, Guid actor, IDbTransaction transaction)
        where T : class, IOrganizationRow
    {
        if (row.IsActive == active)
        {
            return row;
        }

        var id = row.Id;
        var version = row.Version;
        var query = $"""
            UPDATE {EntityMapper.TbName<T>()}
            SET {IsActive} = {EntityMapper.ParameterName(nameof(active))}, {Touch}
            WHERE {Id} = {EntityMapper.ParameterName(nameof(id))} AND {Version} = {EntityMapper.ParameterName(nameof(version))}
            RETURNING *
            """;
        return await SaveAsync<T>(query, new { active, id, version }, actor, transaction);
    }

    /// <summary>Выполняет изменение и сохраняет снимок той же транзакцией</summary>
    /// <typeparam name="T">Тип изменённой записи</typeparam>
    /// <param name="query">Доверенный параметризованный запрос</param>
    /// <param name="parameters">Значения параметров</param>
    /// <param name="actor">Инициатор</param>
    /// <param name="transaction">Текущая транзакция</param>
    /// <returns>Записанная версия</returns>
    /// <exception cref="IdentityCommandException">Конкурирующая запись изменила версию</exception>
    private async Task<T> SaveAsync<T>(string query, object parameters, Guid actor, IDbTransaction transaction)
        where T : class, IOrganizationRow
    {
        var row = await connection.QuerySingleOrDefaultAsync<T>(query, parameters, transaction)
            ?? throw new IdentityCommandException(409, OrganizationErrors.StaleVersion);
        return await RecordAsync(row, actor, transaction);
    }
}
