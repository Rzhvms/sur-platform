using Infrastructure.Constants;
using Core.Database.DapperExtensions.EntityMapper;
using System.Data;
using Application.Constants;
using Application.Models;
using Application.Ports.Repositories;
using Dapper;
using Domain.DalModels;

namespace Infrastructure.Repositories.Sessions;

/// <inheritdoc/>
public sealed class SessionRepository : ISessionRepository
{
    private readonly IDbConnection _dbConnection;

    ///
    public SessionRepository(IDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    /// <summary>
    /// Сохраняет хеш токена в транзакции записи сессии
    /// </summary>
    /// <param name="token">Новый токен без исходного секрета</param>
    /// <param name="transaction">Транзакция сессии</param>
    private async Task InsertRefreshAsync(RefreshTokenDal token, IDbTransaction transaction)
    {
        var insertRefreshQuery = $"""
            INSERT INTO {EntityMapper.TbName<RefreshTokenDal>()} ({EntityMapper.ColName<RefreshTokenDal>(x => x.Id)}, {EntityMapper.ColName<RefreshTokenDal>(x => x.SessionId)}, {EntityMapper.ColName<RefreshTokenDal>(x => x.TokenHash)}, {EntityMapper.ColName<RefreshTokenDal>(x => x.CreatedAt)}, {EntityMapper.ColName<RefreshTokenDal>(x => x.ExpiresAt)}, {EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)})
            VALUES({EntityMapper.ParameterName(nameof(RefreshTokenDal.Id))}, {EntityMapper.ParameterName(nameof(RefreshTokenDal.SessionId))}, {EntityMapper.ParameterName(nameof(RefreshTokenDal.TokenHash))}, {EntityMapper.ParameterName(nameof(RefreshTokenDal.CreatedAt))}, {EntityMapper.ParameterName(nameof(RefreshTokenDal.ExpiresAt))}, {EntityMapper.ParameterName(nameof(RefreshTokenDal.RevokedAt))})
            """;
        await _dbConnection.ExecuteAsync(insertRefreshQuery, token, transaction);
    }

    /// <inheritdoc/>
    public async Task<bool> CreateAsync(SessionDal session, RefreshTokenDal token, long accessVersion)
    {
        using var transaction = _dbConnection.BeginTransaction();
        if (!await LockUserAsync(session.UserId, accessVersion, transaction))
        {
            return false;
        }

        var insertQuery = $"""
            INSERT INTO {EntityMapper.TbName<SessionDal>()}({EntityMapper.ColName<SessionDal>(x => x.Id)},{EntityMapper.ColName<SessionDal>(x => x.UserId)},{EntityMapper.ColName<SessionDal>(x => x.CreatedAt)},{EntityMapper.ColName<SessionDal>(x => x.UpdatedAt)},{EntityMapper.ColName<SessionDal>(x => x.ExpiresAt)},{EntityMapper.ColName<SessionDal>(x => x.RevokedAt)},{EntityMapper.ColName<SessionDal>(x => x.RevokeReason)},{EntityMapper.ColName<SessionDal>(x => x.DeviceName)})
            VALUES({EntityMapper.ParameterName(nameof(SessionDal.Id))},{EntityMapper.ParameterName(nameof(SessionDal.UserId))},{EntityMapper.ParameterName(nameof(SessionDal.CreatedAt))},{EntityMapper.ParameterName(nameof(SessionDal.UpdatedAt))},{EntityMapper.ParameterName(nameof(SessionDal.ExpiresAt))},{EntityMapper.ParameterName(nameof(SessionDal.RevokedAt))},{EntityMapper.ParameterName(nameof(SessionDal.RevokeReason))},{EntityMapper.ParameterName(nameof(SessionDal.DeviceName))})
            """;
        await _dbConnection.ExecuteAsync(insertQuery, session, transaction);
        await InsertRefreshAsync(token, transaction);
        transaction.Commit();
        return true;
    }

    /// <inheritdoc/>
    public Task<SessionDal?> GetAsync(Guid sessionId)
    {
        var selectSessionByIdQuery = $"SELECT * FROM {EntityMapper.TbName<SessionDal>()} WHERE {EntityMapper.ColName<SessionDal>(x => x.Id)}={EntityMapper.ParameterName(SqlParameterNames.SessionId)}";
        return _dbConnection.QuerySingleOrDefaultAsync<SessionDal>(selectSessionByIdQuery, new { sessionId });
    }

    /// <inheritdoc/>
    public async Task<bool> IsActiveAsync(Guid userId, Guid sessionId, DateTime now, CancellationToken cancellationToken, long accessVersion, string? rightCode = null)
    {
        var checkAccessQuery = $"""
            SELECT EXISTS(
                SELECT 1 FROM {EntityMapper.TbName<SessionDal>()} s
                JOIN {EntityMapper.TbName<UserProfileDal>()} u
                    ON u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = s.{EntityMapper.ColName<SessionDal>(x => x.UserId)}
                WHERE s.{EntityMapper.ColName<SessionDal>(x => x.Id)} = {EntityMapper.ParameterName(SqlParameterNames.SessionId)}
                    AND s.{EntityMapper.ColName<SessionDal>(x => x.UserId)} = {EntityMapper.ParameterName(SqlParameterNames.UserId)}
                    AND s.{EntityMapper.ColName<SessionDal>(x => x.RevokedAt)} IS NULL
                    AND s.{EntityMapper.ColName<SessionDal>(x => x.ExpiresAt)} > {EntityMapper.ParameterName(SqlParameterNames.Now)}
                    AND u.{EntityMapper.ColName<UserProfileDal>(x => x.IsActive)} = true
                    AND u.{EntityMapper.ColName<UserProfileDal>(x => x.AccessVersion)} = {EntityMapper.ParameterName(SqlParameterNames.AccessVersion)}
                    AND (NOT u.{EntityMapper.ColName<UserProfileDal>(x => x.MustChangePassword)} OR u.{EntityMapper.ColName<UserProfileDal>(x => x.TemporaryPasswordExpiresAt)} > {EntityMapper.ParameterName(SqlParameterNames.Now)})
                    AND (CAST({EntityMapper.ParameterName(SqlParameterNames.RightCode)} AS text) IS NULL OR (NOT u.{EntityMapper.ColName<UserProfileDal>(x => x.MustChangePassword)}
                        AND EXISTS (SELECT FROM {EntityMapper.TbName<RightDal>()} wanted WHERE wanted.{EntityMapper.ColName<RightDal>(x => x.Code)} = {EntityMapper.ParameterName(SqlParameterNames.RightCode)})
                        AND EXISTS (
                            SELECT FROM {EntityMapper.TbName<UserRoleDal>()} a JOIN {EntityMapper.TbName<RoleDal>()} r ON r.{EntityMapper.ColName<RoleDal>(x => x.Id)} = a.{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}
                            JOIN {EntityMapper.TbName<RoleRightDal>()} rr ON rr.{EntityMapper.ColName<RoleRightDal>(x => x.RoleId)} = r.{EntityMapper.ColName<RoleDal>(x => x.Id)}
                            JOIN {EntityMapper.TbName<RightDal>()} p ON p.{EntityMapper.ColName<RightDal>(x => x.Id)} = rr.{EntityMapper.ColName<RoleRightDal>(x => x.RightId)}
                            WHERE a.{EntityMapper.ColName<UserRoleDal>(x => x.UserId)} = {EntityMapper.ParameterName(SqlParameterNames.UserId)} AND a.{EntityMapper.ColName<UserRoleDal>(x => x.IsActive)} AND r.{EntityMapper.ColName<RoleDal>(x => x.IsActive)}
                                AND r.{EntityMapper.ColName<RoleDal>(x => x.Code)} IS NOT NULL AND (p.{EntityMapper.ColName<RightDal>(x => x.Code)} = {EntityMapper.ParameterName(SqlParameterNames.RightCode)} OR p.{EntityMapper.ColName<RightDal>(x => x.Id)} = {EntityMapper.ParameterName(SqlParameterNames.Admin)})))))
            """;
        var parameters = new { userId, sessionId, now, accessVersion, rightCode, admin = Guid.Parse(IdentityRights.Admin) };
        var command = new CommandDefinition(checkAccessQuery, parameters, cancellationToken: cancellationToken);

        return await _dbConnection.QuerySingleAsync<bool>(command);
    }

    /// <inheritdoc/>
    public Task<RefreshTokenDal?> GetRefreshAsync(string hash)
    {
        var selectRefreshByHashQuery = $"SELECT * FROM {EntityMapper.TbName<RefreshTokenDal>()} WHERE {EntityMapper.ColName<RefreshTokenDal>(x => x.TokenHash)}={EntityMapper.ParameterName(SqlParameterNames.Hash)}";
        return _dbConnection.QuerySingleOrDefaultAsync<RefreshTokenDal>(selectRefreshByHashQuery, new { hash });
    }

    /// <inheritdoc/>
    public async Task<RefreshSessionData?> GetRefreshSessionAsync(string tokenHash)
    {
        var selectRefreshSessionQuery = $"""
            SELECT t.{EntityMapper.ColName<RefreshTokenDal>(x => x.Id)} AS {EntityMapper.ColName<RefreshSessionData>(x => x.TokenId)},
                t.{EntityMapper.ColName<RefreshTokenDal>(x => x.UsedAt)},
                t.{EntityMapper.ColName<RefreshTokenDal>(x => x.SessionId)},
                s.{EntityMapper.ColName<SessionDal>(x => x.UserId)},
                t.{EntityMapper.ColName<RefreshTokenDal>(x => x.ExpiresAt)} AS {EntityMapper.ColName<RefreshSessionData>(x => x.TokenExpiresAt)},
                t.{EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)} AS {EntityMapper.ColName<RefreshSessionData>(x => x.TokenRevokedAt)},
                s.{EntityMapper.ColName<SessionDal>(x => x.ExpiresAt)} AS {EntityMapper.ColName<RefreshSessionData>(x => x.SessionExpiresAt)},
                s.{EntityMapper.ColName<SessionDal>(x => x.RevokedAt)} AS {EntityMapper.ColName<RefreshSessionData>(x => x.SessionRevokedAt)},
                u.{EntityMapper.ColName<UserProfileDal>(x => x.IsActive)} AS {EntityMapper.ColName<RefreshSessionData>(x => x.IsUserActive)}
            FROM {EntityMapper.TbName<RefreshTokenDal>()} t
            JOIN {EntityMapper.TbName<SessionDal>()} s
                ON s.{EntityMapper.ColName<SessionDal>(x => x.Id)} = t.{EntityMapper.ColName<RefreshTokenDal>(x => x.SessionId)}
            JOIN {EntityMapper.TbName<UserProfileDal>()} u
                ON u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = s.{EntityMapper.ColName<SessionDal>(x => x.UserId)}
            WHERE t.{EntityMapper.ColName<RefreshTokenDal>(x => x.TokenHash)} = {EntityMapper.ParameterName(SqlParameterNames.TokenHash)}
            """;
        return await _dbConnection.QuerySingleOrDefaultAsync<RefreshSessionData>(selectRefreshSessionQuery, new { tokenHash });
    }

    /// <inheritdoc/>
    public async Task<bool> RotateAsync(Guid tokenId, RefreshTokenDal next, DateTime now, long accessVersion)
    {
        using var transaction = _dbConnection.BeginTransaction();
        var selectSessionQuery = $"""SELECT * FROM {EntityMapper.TbName<SessionDal>()} WHERE {EntityMapper.ColName<SessionDal>(x => x.Id)} = {EntityMapper.ParameterName(SqlParameterNames.SessionId)}""";
        var session = await _dbConnection.QuerySingleOrDefaultAsync<SessionDal>(
            selectSessionQuery, new { next.SessionId }, transaction);
        if (session is null || !await LockUserAsync(session.UserId, accessVersion, transaction))
        {
            return false;
        }

        var lockSessionQuery = $"""SELECT * FROM {EntityMapper.TbName<SessionDal>()} WHERE {EntityMapper.ColName<SessionDal>(x => x.Id)} = {EntityMapper.ParameterName(SqlParameterNames.SessionId)} FOR UPDATE""";
        session = await _dbConnection.QuerySingleOrDefaultAsync<SessionDal>(
            lockSessionQuery, new { next.SessionId }, transaction);
        if (session is null || session.RevokedAt is not null || session.ExpiresAt <= now)
        {
            return false;
        }

        var selectRefreshTokenQuery = $"""SELECT * FROM {EntityMapper.TbName<RefreshTokenDal>()} WHERE {EntityMapper.ColName<RefreshTokenDal>(x => x.Id)} = {EntityMapper.ParameterName(SqlParameterNames.TokenId)} AND {EntityMapper.ColName<RefreshTokenDal>(x => x.SessionId)} = {EntityMapper.ParameterName(SqlParameterNames.SessionId)}""";
        var previous = await _dbConnection.QuerySingleOrDefaultAsync<RefreshTokenDal>(
            selectRefreshTokenQuery,
            new { tokenId, next.SessionId }, transaction);
        if (previous?.UsedAt is not null)
        {
            var updateSessionQuery = $"""
                UPDATE {EntityMapper.TbName<SessionDal>()} SET {EntityMapper.ColName<SessionDal>(x => x.RevokedAt)} = {EntityMapper.ParameterName(SqlParameterNames.Now)}, {EntityMapper.ColName<SessionDal>(x => x.UpdatedAt)} = {EntityMapper.ParameterName(SqlParameterNames.Now)}, {EntityMapper.ColName<SessionDal>(x => x.RevokeReason)} = {EntityMapper.ParameterName(SqlParameterNames.Reason)},
                                    {EntityMapper.ColName<SessionDal>(x => x.Version)} = gen_random_uuid() WHERE {EntityMapper.ColName<SessionDal>(x => x.Id)} = {EntityMapper.ParameterName(SqlParameterNames.SessionId)}
                """;
            await _dbConnection.ExecuteAsync(updateSessionQuery, new { next.SessionId, now, reason = SessionRevokeReasons.RefreshReuse }, transaction);
            var updateRefreshTokenQuery2 = $"""
                UPDATE {EntityMapper.TbName<RefreshTokenDal>()} SET {EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)} = {EntityMapper.ParameterName(SqlParameterNames.Now)} WHERE {EntityMapper.ColName<RefreshTokenDal>(x => x.SessionId)} = {EntityMapper.ParameterName(SqlParameterNames.SessionId)} AND {EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)} IS NULL
                """;
            await _dbConnection.ExecuteAsync(updateRefreshTokenQuery2, new { next.SessionId, now, reason = SessionRevokeReasons.RefreshReuse }, transaction);
            transaction.Commit();
            return false;
        }

        if (previous is null || previous.RevokedAt is not null || previous.ExpiresAt <= now)
        {
            return false;
        }

        await InsertRefreshAsync(next, transaction);
        var updateRefreshTokenQuery = $"""
            UPDATE {EntityMapper.TbName<RefreshTokenDal>()} SET {EntityMapper.ColName<RefreshTokenDal>(x => x.UsedAt)} = {EntityMapper.ParameterName(SqlParameterNames.Now)}, {EntityMapper.ColName<RefreshTokenDal>(x => x.ReplacedById)} = {EntityMapper.ParameterName(SqlParameterNames.Id)} WHERE {EntityMapper.ColName<RefreshTokenDal>(x => x.Id)} = {EntityMapper.ParameterName(SqlParameterNames.TokenId)}
            """;
        await _dbConnection.ExecuteAsync(updateRefreshTokenQuery, new { tokenId, id = next.Id, now }, transaction);
        var updateSessionQuery2 = $"""
            UPDATE {EntityMapper.TbName<SessionDal>()} SET {EntityMapper.ColName<SessionDal>(x => x.UpdatedAt)} = {EntityMapper.ParameterName(SqlParameterNames.Now)}, {EntityMapper.ColName<SessionDal>(x => x.Version)} = gen_random_uuid() WHERE {EntityMapper.ColName<SessionDal>(x => x.Id)} = {EntityMapper.ParameterName(SqlParameterNames.SessionId)}
            """;
        await _dbConnection.ExecuteAsync(updateSessionQuery2, new { next.SessionId, now }, transaction);
        transaction.Commit();
        return true;
    }

    /// <summary>
    /// Согласует запись сессии с изменением учётных данных
    /// </summary>
    /// <param name="userId">Владелец сессии</param>
    /// <param name="accessVersion">Версия проверенных прав или пароля</param>
    /// <param name="transaction">Транзакция записи сессии</param>
    /// <returns>Пользователь активен и его версия совпадает</returns>
    private Task<bool> LockUserAsync(Guid userId, long accessVersion, IDbTransaction transaction)
    {
        var selectUserProfileQuery = $"""
            SELECT EXISTS (SELECT FROM {EntityMapper.TbName<UserProfileDal>()} WHERE {EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = {EntityMapper.ParameterName(SqlParameterNames.UserId)} AND {EntityMapper.ColName<UserProfileDal>(x => x.IsActive)}
                AND {EntityMapper.ColName<UserProfileDal>(x => x.AccessVersion)} = {EntityMapper.ParameterName(SqlParameterNames.AccessVersion)} AND
                (NOT {EntityMapper.ColName<UserProfileDal>(x => x.MustChangePassword)} OR {EntityMapper.ColName<UserProfileDal>(x => x.TemporaryPasswordExpiresAt)} > now()) FOR UPDATE)
            """;
        return _dbConnection.ExecuteScalarAsync<bool>(selectUserProfileQuery, new { userId, accessVersion }, transaction);
    }

    /// <inheritdoc/>
    public async Task<bool> RevokeAsync(Guid sessionId, Guid userId, DateTime now, string reason)
    {
        using var transaction = _dbConnection.BeginTransaction();

        var updateQuery = $"""
            UPDATE {EntityMapper.TbName<SessionDal>()}
            SET {EntityMapper.ColName<SessionDal>(x => x.RevokedAt)} = {EntityMapper.ParameterName(SqlParameterNames.Now)},
                {EntityMapper.ColName<SessionDal>(x => x.RevokeReason)} = {EntityMapper.ParameterName(SqlParameterNames.Reason)},
                {EntityMapper.ColName<SessionDal>(x => x.UpdatedAt)} = {EntityMapper.ParameterName(SqlParameterNames.Now)},
                {EntityMapper.ColName<SessionDal>(x => x.Version)} = gen_random_uuid()
            WHERE {EntityMapper.ColName<SessionDal>(x => x.Id)} = {EntityMapper.ParameterName(SqlParameterNames.SessionId)}
                AND {EntityMapper.ColName<SessionDal>(x => x.UserId)} = {EntityMapper.ParameterName(SqlParameterNames.UserId)}
                AND {EntityMapper.ColName<SessionDal>(x => x.RevokedAt)} IS NULL
            """;
        var affected = await _dbConnection.ExecuteAsync(updateQuery, new { sessionId, userId, now, reason }, transaction);

        if (affected != 1)
        {
            return false;
        }

        var updateCommandQuery = $"""
            UPDATE {EntityMapper.TbName<RefreshTokenDal>()} SET {EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)}={EntityMapper.ParameterName(SqlParameterNames.Now)} WHERE {EntityMapper.ColName<RefreshTokenDal>(x => x.SessionId)}={EntityMapper.ParameterName(SqlParameterNames.SessionId)} AND {EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)} IS NULL;
            """;
        await _dbConnection.ExecuteAsync(updateCommandQuery, new { sessionId, now }, transaction);

        transaction.Commit();
        return true;
    }

    /// <inheritdoc/>
    public async Task RevokeUserSessionsAsync(Guid userId, DateTime now, string reason)
    {
        using var transaction = _dbConnection.BeginTransaction();
        var updateQuery = $"""
            UPDATE {EntityMapper.TbName<SessionDal>()} SET {EntityMapper.ColName<SessionDal>(x => x.RevokedAt)}={EntityMapper.ParameterName(SqlParameterNames.Now)},{EntityMapper.ColName<SessionDal>(x => x.RevokeReason)}={EntityMapper.ParameterName(SqlParameterNames.Reason)},{EntityMapper.ColName<SessionDal>(x => x.UpdatedAt)}={EntityMapper.ParameterName(SqlParameterNames.Now)} WHERE {EntityMapper.ColName<SessionDal>(x => x.UserId)}={EntityMapper.ParameterName(SqlParameterNames.UserId)} AND {EntityMapper.ColName<SessionDal>(x => x.RevokedAt)} IS NULL
            """;
        await _dbConnection.ExecuteAsync(updateQuery, new { userId, now, reason }, transaction);
        var updateQuery2 = $"""
            UPDATE {EntityMapper.TbName<RefreshTokenDal>()} SET {EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)}={EntityMapper.ParameterName(SqlParameterNames.Now)} WHERE {EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)} IS NULL
                        AND {EntityMapper.ColName<RefreshTokenDal>(x => x.SessionId)} IN (SELECT {EntityMapper.ColName<SessionDal>(x => x.Id)} FROM {EntityMapper.TbName<SessionDal>()} WHERE {EntityMapper.ColName<SessionDal>(x => x.UserId)}={EntityMapper.ParameterName(SqlParameterNames.UserId)})
            """;
        await _dbConnection.ExecuteAsync(updateQuery2, new { userId, now, reason }, transaction);
        transaction.Commit();
    }
}
