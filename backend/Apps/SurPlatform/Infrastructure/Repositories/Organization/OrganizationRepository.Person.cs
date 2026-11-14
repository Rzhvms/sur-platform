using System.Data;
using Application.Constants;
using Application.Exceptions;
using Application.UseCases.Organization.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;

namespace Infrastructure.Repositories.Organization;

/// <summary>Хранение сотрудников и проверка зависимостей сотрудника</summary>
internal sealed partial class OrganizationRepository
{
    /// <summary>Идентификатор учётной записи сотрудника</summary>
    private static readonly string PersonUser = EntityMapper.ColName<OrgPersonDal>(x => x.UserId);

    /// <summary>Неизменяемая основная дирекция сотрудника</summary>
    private static readonly string PersonDirectorate = EntityMapper.ColName<OrgPersonDal>(x => x.DirectorateId);

    /// <summary>Должность сотрудника</summary>
    private static readonly string PersonPosition = EntityMapper.ColName<OrgPersonDal>(x => x.PositionName);

    /// <summary>Связь справочника сотрудников с текущими данными пользователя</summary>
    private static readonly string PersonJoin =
        $"JOIN {EntityMapper.TbName<UserProfileDal>()} u ON u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = t.{PersonUser}";

    /// <summary>Отображаемые атрибуты из учётной записи</summary>
    private static readonly string PersonProjection =
        $", u.{EntityMapper.ColName<UserProfileDal>(x => x.FullName)}, u.{EntityMapper.ColName<UserProfileDal>(x => x.Email)}";

    /// <inheritdoc/>
    public Task<OrgPersonDal[]> ListPersonAsync(Guid after, int limit, bool? active, string? search, Guid? directorateId)
    {
        var display = $"concat_ws(' ', u.{EntityMapper.ColName<UserProfileDal>(x => x.FullName)}, u.{EntityMapper.ColName<UserProfileDal>(x => x.Email)}, t.{PersonPosition})";
        return ListAsync<OrgPersonDal>(after, limit, active, search, directorateId, display, PersonDirectorate, PersonJoin, PersonProjection);
    }

    /// <inheritdoc/>
    public Task<OrgPersonDal> GetPersonAsync(Guid id)
    {
        return ReadPersonAsync(id, null);
    }

    /// <inheritdoc/>
    public Task<OrgPersonDal> CreatePersonAsync(Guid actor, Guid sessionId, PersonRequest request)
    {
        return WriteAsync(actor, sessionId, transaction =>
        {
            return InsertPersonAsync(request.UserId, request.DirectorateId, request.PositionName, actor, transaction);
        });
    }

    /// <inheritdoc/>
    public Task<OrgPersonDal> UpdatePersonAsync(Guid actor, Guid sessionId, Guid id, PersonEditRequest request)
    {
        return WriteAsync(actor, sessionId, async transaction =>
        {
            var row = await ReadPersonAsync(id, transaction);
            var version = request.Version;
            RequireVersion(row, version);
            if (!row.IsActive)
            {
                throw new IdentityCommandException(409, OrganizationErrors.Ineligible);
            }

            var positionName = request.PositionName?.Trim();
            var query = $"""
                UPDATE {EntityMapper.TbName<OrgPersonDal>()}
                SET {PersonPosition} = {EntityMapper.ParameterName(nameof(positionName))}, {Touch}
                WHERE {Id} = {EntityMapper.ParameterName(nameof(id))} AND {Version} = {EntityMapper.ParameterName(nameof(version))}
                """;
            if (await connection.ExecuteAsync(query, new { id, version, positionName }, transaction) != 1)
            {
                throw new IdentityCommandException(409, OrganizationErrors.StaleVersion);
            }

            return await RecordAsync(await ReadPersonAsync(id, transaction), actor, transaction);
        });
    }

    /// <inheritdoc/>
    public Task<OrgPersonDal> SetPersonStateAsync(Guid actor, Guid sessionId, Guid id, OrgStateRequest request)
    {
        return WriteAsync(actor, sessionId, async transaction =>
        {
            if (!request.IsActive && request.AssignCeo)
            {
                throw new IdentityCommandException(400, OrganizationErrors.Invalid);
            }

            var row = await ReadPersonAsync(id, transaction);
            RequireVersion(row, request.Version);
            if (!request.IsActive)
            {
                await EnsureNoPersonDependenciesAsync(id, transaction);
            }
            else if (!row.IsActive || request.AssignCeo)
            {
                await RequireUserRoleAsync(row.UserId, null, transaction);
                if (row.DirectorateId is Guid directorateId)
                {
                    await RequireDirectorateAsync(directorateId, transaction);
                }
                else if (!request.AssignCeo)
                {
                    throw new IdentityCommandException(409, OrganizationErrors.Ineligible);
                }
            }

            var state = await SetStateAsync(row, request.IsActive, actor, transaction);
            if (request.IsActive && request.AssignCeo)
            {
                await RequireFreeSlotAsync(OrganizationRoles.Ceo, null, null, transaction);
                await InsertResponsibilityAsync(state, OrganizationRoles.Ceo, null, actor, transaction);
            }

            return state == row ? row : await ReadPersonAsync(id, transaction);
        });
    }

    /// <summary>Читает сотрудника с отображаемыми данными связанного пользователя</summary>
    /// <param name="id">Идентификатор сотрудника</param>
    /// <param name="transaction">Текущая транзакция, если выполняется запись</param>
    /// <returns>Карточка сотрудника</returns>
    private async Task<OrgPersonDal> ReadPersonAsync(Guid id, IDbTransaction? transaction)
    {
        var query = $"""
            SELECT t.*{PersonProjection} FROM {EntityMapper.TbName<OrgPersonDal>()} t {PersonJoin}
            WHERE t.{Id} = {EntityMapper.ParameterName(nameof(id))}
            """;
        return await connection.QuerySingleOrDefaultAsync<OrgPersonDal>(query, new { id }, transaction)
            ?? throw new IdentityCommandException(404, OrganizationErrors.NotFound);
    }

    /// <summary>Создаёт сотрудника с неизменяемой привязкой к учётной записи и дирекции</summary>
    /// <param name="userId">Активная учётная запись</param>
    /// <param name="directorateId">Основная дирекция, необязательная в команде CEO</param>
    /// <param name="positionName">Должность</param>
    /// <param name="actor">Администратор</param>
    /// <param name="transaction">Общая транзакция Identity и оргструктуры</param>
    /// <returns>Новая запись сотрудника</returns>
    private async Task<OrgPersonDal> InsertPersonAsync(Guid userId, Guid? directorateId, string? positionName,
        Guid actor, IDbTransaction transaction)
    {
        await RequireUserRoleAsync(userId, null, transaction);
        if (directorateId is Guid directorate)
        {
            await RequireDirectorateAsync(directorate, transaction);
        }

        positionName = positionName?.Trim();
        var query = $"""
            INSERT INTO {EntityMapper.TbName<OrgPersonDal>()} ({Id}, {PersonUser}, {PersonDirectorate}, {PersonPosition})
            VALUES (gen_random_uuid(), {EntityMapper.ParameterName(nameof(userId))},
                {EntityMapper.ParameterName(nameof(directorateId))}, {EntityMapper.ParameterName(nameof(positionName))})
            RETURNING {Id}
            """;
        var id = await connection.QuerySingleAsync<Guid>(query, new { userId, directorateId, positionName }, transaction);
        return await RecordAsync(await ReadPersonAsync(id, transaction), actor, transaction);
    }

    /// <summary>Проверяет активность пользователя и наличие действующего назначения требуемой роли</summary>
    /// <param name="userId">Связанная учётная запись</param>
    /// <param name="roleCode">Код требуемой роли либо null для проверки активности</param>
    /// <param name="transaction">Транзакция, защищённая административной блокировкой</param>
    private async Task RequireUserRoleAsync(Guid userId, string? roleCode, IDbTransaction transaction)
    {
        var systemId = IdentityAccounts.SystemUserId;
        var query = $"""
            SELECT EXISTS (
                SELECT FROM {EntityMapper.TbName<UserProfileDal>()} u
                WHERE u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = {EntityMapper.ParameterName(nameof(userId))}
                  AND u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} <> {EntityMapper.ParameterName(nameof(systemId))}
                  AND u.{EntityMapper.ColName<UserProfileDal>(x => x.IsActive)}
                  AND (CAST({EntityMapper.ParameterName(nameof(roleCode))} AS text) IS NULL OR EXISTS (
                    SELECT FROM {EntityMapper.TbName<UserRoleDal>()} ur
                    JOIN {EntityMapper.TbName<RoleDal>()} r
                      ON r.{EntityMapper.ColName<RoleDal>(x => x.Id)} = ur.{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}
                    WHERE ur.{EntityMapper.ColName<UserRoleDal>(x => x.UserId)} = {EntityMapper.ParameterName(nameof(userId))}
                      AND ur.{EntityMapper.ColName<UserRoleDal>(x => x.IsActive)}
                      AND r.{EntityMapper.ColName<RoleDal>(x => x.IsActive)}
                      AND r.{EntityMapper.ColName<RoleDal>(x => x.Code)} = {EntityMapper.ParameterName(nameof(roleCode))}
                  ))
            )
            """;
        if (!await connection.ExecuteScalarAsync<bool>(query, new { userId, roleCode, systemId }, transaction))
        {
            throw new IdentityCommandException(409, OrganizationErrors.Ineligible);
        }
    }

    /// <summary>Деактивирует активную запись сотрудника при отключении его учётной записи</summary>
    /// <param name="userId">Отключаемая учётная запись</param>
    /// <param name="actor">Инициатор отключения</param>
    /// <param name="transaction">Транзакция отключения пользователя</param>
    public async Task DeactivateForUserAsync(Guid userId, Guid actor, IDbTransaction transaction)
    {
        var person = await FindActivePersonAsync(userId, transaction);
        if (person is not null)
        {
            await EnsureNoPersonDependenciesAsync(person.Id, transaction);
            await SetStateAsync(person, false, actor, transaction);
        }
    }

    /// <summary>Ищет активную запись по учётной записи</summary>
    /// <param name="userId">Учётная запись</param>
    /// <param name="transaction">Текущая транзакция</param>
    /// <returns>Активный сотрудник или null</returns>
    private Task<OrgPersonDal?> FindActivePersonAsync(Guid userId, IDbTransaction transaction)
    {
        var query = $"""
            SELECT t.*{PersonProjection} FROM {EntityMapper.TbName<OrgPersonDal>()} t {PersonJoin}
            WHERE t.{PersonUser} = {EntityMapper.ParameterName(nameof(userId))} AND t.{IsActive}
            """;
        return connection.QuerySingleOrDefaultAsync<OrgPersonDal>(query, new { userId }, transaction);
    }

    /// <summary>Блокирует деактивацию при действующем владении или назначении</summary>
    /// <param name="personId">Сотрудник</param>
    /// <param name="transaction">Текущая административная транзакция</param>
    private async Task EnsureNoPersonDependenciesAsync(Guid personId, IDbTransaction transaction)
    {
        var query = $"""
            SELECT {Id} FROM {EntityMapper.TbName<ProcessDal>()}
            WHERE {IsActive} AND {EntityMapper.ColName<ProcessDal>(x => x.OwnerId)} = {EntityMapper.ParameterName(nameof(personId))}
            UNION ALL
            SELECT {Id} FROM {EntityMapper.TbName<ResponsibilityDal>()}
            WHERE {IsActive} AND {EntityMapper.ColName<ResponsibilityDal>(x => x.PersonId)} = {EntityMapper.ParameterName(nameof(personId))}
            """;
        var ids = (await connection.QueryAsync<Guid>(query, new { personId }, transaction)).ToArray();
        if (ids.Length > 0)
        {
            throw new IdentityCommandException(409, OrganizationErrors.Dependencies, ids);
        }
    }
}
