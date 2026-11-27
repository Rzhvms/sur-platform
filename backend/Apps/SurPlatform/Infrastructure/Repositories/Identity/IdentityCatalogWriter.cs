using System.Data;
using Application.Constants;
using Application.Exceptions;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Constants;
using Npgsql;

namespace Infrastructure.Repositories.Identity;

/// <summary>
/// Синхронизация поставляемых метаданных каталога Identity
/// </summary>
internal static class IdentityCatalogWriter
{
    /// <summary>
    /// HTTP-статус конфликта системного каталога
    /// </summary>
    private const int CatalogConflictStatus = 409;

    /// <summary>
    /// Синхронизирует поставляемые роли и права в транзакции административной команды
    /// </summary>
    /// <param name="connection">Рабочее подключение клиента</param>
    /// <param name="transaction">Транзакция клиента с удерживаемой административной блокировкой</param>
    /// <exception cref="IdentityCommandException">Каталог конфликтует с существующими записями</exception>
    internal static async Task SynchronizeAsync(IDbConnection connection, IDbTransaction transaction)
    {
        try
        {
            await SynchronizeRolesAsync(connection, transaction);
            await SynchronizeRightsAsync(connection, transaction);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new IdentityCommandException(CatalogConflictStatus, IdentityErrorCodes.CatalogConflict);
        }
    }

    /// <summary>
    /// Сопоставляет роли по коду и обновляет только поставляемые метаданные
    /// </summary>
    /// <param name="connection">Рабочее подключение клиента</param>
    /// <param name="transaction">Транзакция обновления каталога</param>
    private static async Task SynchronizeRolesAsync(IDbConnection connection, IDbTransaction transaction)
    {
        var roles = InitialIdentityCatalog.GetRoles();
        var codes = Array.ConvertAll(roles, role => role.Code);
        var names = Array.ConvertAll(roles, role => role.Name);
        var descriptions = Array.ConvertAll(roles, role => role.Description);
        var roleTable = EntityMapper.TbName<RoleDal>();
        var id = EntityMapper.ColName<RoleDal>(role => role.Id);
        var code = EntityMapper.ColName<RoleDal>(role => role.Code);
        var name = EntityMapper.ColName<RoleDal>(role => role.Name);
        var description = EntityMapper.ColName<RoleDal>(role => role.Description);
        var system = EntityMapper.ColName<RoleDal>(role => role.IsSystem);
        var version = EntityMapper.ColName<RoleDal>(role => role.Version);
        var updatedAt = EntityMapper.ColName<RoleDal>(role => role.UpdatedAt);
        var synchronizeRolesQuery = $"""
            INSERT INTO {roleTable} AS stored ({id}, {code}, {name}, {description}, {system})
            SELECT gen_random_uuid(), catalog.{code}, catalog.{name}, catalog.{description}, true
            FROM unnest({EntityMapper.ParameterName(nameof(codes))}, {EntityMapper.ParameterName(nameof(names))},
                {EntityMapper.ParameterName(nameof(descriptions))}) AS catalog ({code}, {name}, {description})
            ON CONFLICT ({code}) DO UPDATE
            SET {name} = excluded.{name},
                {description} = excluded.{description},
                {system} = true,
                {version} = gen_random_uuid(),
                {updatedAt} = now()
            WHERE (stored.{name}, stored.{description}, stored.{system})
                IS DISTINCT FROM (excluded.{name}, excluded.{description}, excluded.{system})
            """;

        await connection.ExecuteAsync(synchronizeRolesQuery, new { codes, names, descriptions }, transaction);
    }

    /// <summary>
    /// Обновляет названия известных прав, добавляет отсутствующие и проверяет идентификатор admin
    /// </summary>
    /// <param name="connection">Рабочее подключение клиента</param>
    /// <param name="transaction">Транзакция обновления каталога</param>
    /// <exception cref="IdentityCommandException">Право admin связано с неожиданным идентификатором</exception>
    private static async Task SynchronizeRightsAsync(IDbConnection connection, IDbTransaction transaction)
    {
        var rights = InitialIdentityCatalog.GetRights();
        var codes = Array.ConvertAll(rights, right => right.Code);
        var names = Array.ConvertAll(rights, right => right.Name);
        var ids = Array.ConvertAll(rights, right => right.Code == IdentityRights.AdminCode
            ? Guid.Parse(IdentityRights.Admin)
            : Guid.NewGuid());
        var rightTable = EntityMapper.TbName<RightDal>();
        var id = EntityMapper.ColName<RightDal>(right => right.Id);
        var code = EntityMapper.ColName<RightDal>(right => right.Code);
        var name = EntityMapper.ColName<RightDal>(right => right.Name);
        var system = EntityMapper.ColName<RightDal>(right => right.IsSystem);
        var synchronizeRightsQuery = $"""
            INSERT INTO {rightTable} AS stored ({id}, {code}, {name}, {system})
            SELECT catalog.{id}, catalog.{code}, catalog.{name}, true
            FROM unnest({EntityMapper.ParameterName(nameof(ids))}, {EntityMapper.ParameterName(nameof(codes))},
                {EntityMapper.ParameterName(nameof(names))}) AS catalog ({id}, {code}, {name})
            ON CONFLICT ({code}) DO UPDATE
            SET {name} = excluded.{name}, {system} = true
            WHERE (stored.{name}, stored.{system}) IS DISTINCT FROM (excluded.{name}, excluded.{system})
            """;

        await connection.ExecuteAsync(synchronizeRightsQuery, new { ids, codes, names }, transaction);

        var adminCode = IdentityRights.AdminCode;
        var checkAdminQuery = $"""
            SELECT {id} FROM {rightTable}
            WHERE {code} = {EntityMapper.ParameterName(nameof(adminCode))}
            """;
        var adminId = await connection.QuerySingleAsync<Guid>(checkAdminQuery, new { adminCode }, transaction);
        if (adminId != Guid.Parse(IdentityRights.Admin))
        {
            throw new IdentityCommandException(CatalogConflictStatus, IdentityErrorCodes.CatalogConflict);
        }
    }
}
