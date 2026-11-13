using Application.Models;
using System.Data;
using Application.Constants;
using Application.Exceptions;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;

namespace Infrastructure.Repositories.Organization;

/// <summary>Проверки, общие для Identity и оргструктуры</summary>
internal sealed partial class OrganizationRepository
{
    /// <summary>Разделитель кода и названия области ответственности</summary>
    private const string ResponsibilitySeparator = " — ";

    /// <inheritdoc/>
    public async Task<OrganizationUserContext> GetUserContextAsync(Guid userId)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        var person = await FindActivePersonAsync(userId, transaction);
        if (person is null)
        {
            transaction.Commit();
            return new OrganizationUserContext(null, [], []);
        }

        var personId = person.Id;
        var processesQuery = $"""
            SELECT * FROM {EntityMapper.TbName<ProcessDal>()}
            WHERE {IsActive} AND {EntityMapper.ColName<ProcessDal>(x => x.OwnerId)} = {EntityMapper.ParameterName(nameof(personId))}
            ORDER BY {Id}
            """;
        var responsibilitiesQuery = $"""
            SELECT * FROM {EntityMapper.TbName<ResponsibilityDal>()}
            WHERE {IsActive} AND {EntityMapper.ColName<ResponsibilityDal>(x => x.PersonId)} = {EntityMapper.ParameterName(nameof(personId))}
            ORDER BY {Id}
            """;
        var processes = await connection.QueryAsync<ProcessDal>(processesQuery, new { personId }, transaction);
        var responsibilities = await connection.QueryAsync<ResponsibilityDal>(responsibilitiesQuery, new { personId }, transaction);
        transaction.Commit();
        return new OrganizationUserContext(person, processes.ToArray(), responsibilities.ToArray());
    }

    /// <summary>Ищет активные организационные связи, оставшиеся без необходимой роли</summary>
    /// <param name="userId">Пользователь при индивидуальном отзыве, иначе null</param>
    /// <param name="roleCode">Деактивируемая роль при массовой проверке, иначе null</param>
    /// <param name="transaction">Транзакция после изменения ролей, но до коммита</param>
    /// <exception cref="IdentityCommandException">Активные связи требуют отозванной роли</exception>
    internal async Task RequireValidRolesAsync(Guid? userId, string? roleCode, IDbTransaction transaction)
    {
        var ownerCode = OrganizationRoles.Owner;
        var query = $"""
            WITH dependencies AS (
                SELECT p.{Id} AS dependency, p.{EntityMapper.ColName<ProcessDal>(x => x.OwnerId)} AS person,
                    CAST({EntityMapper.ParameterName(nameof(ownerCode))} AS text) AS required_role
                FROM {EntityMapper.TbName<ProcessDal>()} p WHERE p.{IsActive}
                UNION ALL
                SELECT a.{Id}, a.{EntityMapper.ColName<ResponsibilityDal>(x => x.PersonId)}, a.{ResponsibilityType}
                FROM {EntityMapper.TbName<ResponsibilityDal>()} a WHERE a.{IsActive}
            )
            SELECT d.dependency FROM dependencies d
            JOIN {EntityMapper.TbName<OrgPersonDal>()} p ON p.{Id} = d.person
            WHERE (CAST({EntityMapper.ParameterName(nameof(userId))} AS uuid) IS NULL OR p.{PersonUser} = {EntityMapper.ParameterName(nameof(userId))})
              AND (CAST({EntityMapper.ParameterName(nameof(roleCode))} AS text) IS NULL OR d.required_role = {EntityMapper.ParameterName(nameof(roleCode))})
              AND NOT EXISTS (
                SELECT FROM {EntityMapper.TbName<UserRoleDal>()} ur
                JOIN {EntityMapper.TbName<RoleDal>()} r ON r.{EntityMapper.ColName<RoleDal>(x => x.Id)} = ur.{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}
                WHERE ur.{EntityMapper.ColName<UserRoleDal>(x => x.UserId)} = p.{PersonUser}
                  AND ur.{EntityMapper.ColName<UserRoleDal>(x => x.IsActive)} AND r.{EntityMapper.ColName<RoleDal>(x => x.IsActive)}
                  AND r.{EntityMapper.ColName<RoleDal>(x => x.Code)} = d.required_role
              )
            """;
        var ids = (await connection.QueryAsync<Guid>(query, new { userId, roleCode, ownerCode }, transaction)).ToArray();
        if (ids.Length > 0)
        {
            throw new IdentityCommandException(409, OrganizationErrors.Dependencies, ids);
        }
    }

    /// <summary>Читает действующие зоны ответственности для письма или административной карточки</summary>
    /// <param name="userId">Получатель доступа</param>
    /// <param name="transaction">Текущая транзакция, если открыта</param>
    /// <returns>Отображаемые зоны ответственности и процессы во владении</returns>
    internal async Task<string[]> ReadResponsibilitiesAsync(Guid userId, IDbTransaction? transaction = null)
    {
        var query = BuildResponsibilityLabelsQuery(EntityMapper.ParameterName(nameof(userId)));
        return (await connection.QueryAsync<string>(query, new { userId }, transaction)).ToArray();
    }

    /// <summary>Формирует выборку действующих зон по доверенному SQL-выражению пользователя</summary>
    /// <param name="userIdExpression">Параметр или колонка внешней выборки</param>
    /// <returns>SQL для карточки пользователя и приглашения</returns>
    internal static string BuildResponsibilityLabelsQuery(string userIdExpression)
    {
        var query = $"""
            SELECT concat_ws('{ResponsibilitySeparator}', a.{ResponsibilityType}, d.{EntityMapper.ColName<DirectorateDal>(x => x.Name)})
            FROM {EntityMapper.TbName<ResponsibilityDal>()} a
            JOIN {EntityMapper.TbName<OrgPersonDal>()} p ON p.{Id} = a.{EntityMapper.ColName<ResponsibilityDal>(x => x.PersonId)}
            LEFT JOIN {EntityMapper.TbName<DirectorateDal>()} d ON d.{Id} = a.{ResponsibilityDirectorate}
            WHERE a.{IsActive} AND p.{IsActive} AND p.{PersonUser} = {userIdExpression}
            UNION ALL
            SELECT concat_ws('{ResponsibilitySeparator}', '{OrganizationRoles.Owner}', pr.{EntityMapper.ColName<ProcessDal>(x => x.Name)})
            FROM {EntityMapper.TbName<ProcessDal>()} pr
            JOIN {EntityMapper.TbName<OrgPersonDal>()} p ON p.{Id} = pr.{EntityMapper.ColName<ProcessDal>(x => x.OwnerId)}
            WHERE pr.{IsActive} AND p.{IsActive} AND p.{PersonUser} = {userIdExpression}
            ORDER BY 1
            """;
        return query;
    }

    /// <summary>Сохраняет срез ФИО для отчётов после изменения карточки пользователя</summary>
    /// <param name="userId">Изменённая учётная запись</param>
    /// <param name="actor">Администратор</param>
    /// <param name="transaction">Транзакция изменения ФИО</param>
    internal async Task SnapshotPersonNameAsync(Guid userId, Guid actor, IDbTransaction transaction)
    {
        var person = await FindActivePersonAsync(userId, transaction);
        if (person is null)
        {
            return;
        }

        var id = person.Id;
        var query = $"""
            UPDATE {EntityMapper.TbName<OrgPersonDal>()} SET {Touch}
            WHERE {Id} = {EntityMapper.ParameterName(nameof(id))}
            """;
        await connection.ExecuteAsync(query, new { id }, transaction);
        await RecordAsync(await ReadPersonAsync(id, transaction), actor, transaction);
    }
}
