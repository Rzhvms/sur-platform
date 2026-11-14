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
/// Общая транзакционная граница административных записей клиента
/// </summary>
internal static class AdministrationTransaction
{
    /// <summary>
    /// Общий ключ для записей ролей и учётных записей в клиентской базе
    /// </summary>
    private const long AdministrationLock = 202611140100;

    /// <summary>
    /// Сериализует административные команды и повторно проверяет полномочия внутри транзакции
    /// </summary>
    /// <typeparam name="T">Результат записи</typeparam>
    /// <param name="connection">Подключение выбранного клиента</param>
    /// <param name="actor">Инициатор изменения</param>
    /// <param name="sessionId">Сессия администратора, если команда пришла по HTTP</param>
    /// <param name="action">Изменения в одной транзакции</param>
    /// <returns>Результат сохранённой команды</returns>
    internal static async Task<T> WriteAsync<T>(IDbConnection connection, Guid actor, Guid? sessionId, Func<IDbTransaction, Task<T>> action)
    {
        using var transaction = connection.BeginTransaction();
        await LockAsync(connection, transaction);
        var checkActorQuery = $"""
            SELECT EXISTS (SELECT FROM {EntityMapper.TbName<UserProfileDal>()} u JOIN {EntityMapper.TbName<SessionDal>()} s ON s.{EntityMapper.ColName<SessionDal>(x => x.UserId)} = u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)}
                JOIN {EntityMapper.TbName<UserRoleDal>()} a ON a.{EntityMapper.ColName<UserRoleDal>(x => x.UserId)} = u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} JOIN {EntityMapper.TbName<RoleDal>()} r ON r.{EntityMapper.ColName<RoleDal>(x => x.Id)} = a.{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}
                JOIN {EntityMapper.TbName<RoleRightDal>()} rr ON rr.{EntityMapper.ColName<RoleRightDal>(x => x.RoleId)} = r.{EntityMapper.ColName<RoleDal>(x => x.Id)}
                WHERE u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = {EntityMapper.ParameterName(SqlParameterNames.Actor)} AND u.{EntityMapper.ColName<UserProfileDal>(x => x.IsActive)} AND NOT u.{EntityMapper.ColName<UserProfileDal>(x => x.MustChangePassword)}
                    AND s.{EntityMapper.ColName<SessionDal>(x => x.Id)} = {EntityMapper.ParameterName(SqlParameterNames.SessionId)} AND s.{EntityMapper.ColName<SessionDal>(x => x.RevokedAt)} IS NULL AND s.{EntityMapper.ColName<SessionDal>(x => x.ExpiresAt)} > clock_timestamp()
                    AND a.{EntityMapper.ColName<UserRoleDal>(x => x.IsActive)} AND r.{EntityMapper.ColName<RoleDal>(x => x.IsActive)} AND r.{EntityMapper.ColName<RoleDal>(x => x.Code)} IS NOT NULL AND rr.{EntityMapper.ColName<RoleRightDal>(x => x.RightId)} = {EntityMapper.ParameterName(SqlParameterNames.Admin)})
            """;
        if (sessionId is not null && !await connection.ExecuteScalarAsync<bool>(checkActorQuery, new { actor, sessionId, admin = Guid.Parse(IdentityRights.Admin) }, transaction))
        {
            throw new IdentityCommandException(403, IdentityErrorCodes.AccessDenied);
        }

        try
        {
            var result = await action(transaction);
            transaction.Commit();
            return result;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation && ex.ConstraintName == IdentityIndexes.NormalizedEmail)
        {
            throw new IdentityCommandException(409, IdentityErrorCodes.EmailAlreadyExists);
        }
    }

    /// <summary>Сериализует организационные и административные команды клиента</summary>
    /// <param name="connection">Открытое подключение клиента</param>
    /// <param name="transaction">Транзакция, на срок которой устанавливается блокировка</param>
    internal static async Task LockAsync(IDbConnection connection, IDbTransaction transaction)
    {
        var lockAdministrationQuery = $"SELECT pg_advisory_xact_lock({EntityMapper.ParameterName(SqlParameterNames.Key)})";
        await connection.ExecuteAsync(lockAdministrationQuery, new { key = AdministrationLock }, transaction);
    }
}
