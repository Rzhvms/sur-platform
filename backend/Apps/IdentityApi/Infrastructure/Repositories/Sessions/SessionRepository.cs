using Core.Database.DapperExtensions.EntityMapper;
using System.Data;
using Application.Constants;
using Application.Ports.Repositories;
using Dapper;
using Domain.DalModels;

namespace Infrastructure.Repositories.Sessions;

/// <summary>
/// Хранение сессий и ротация refresh-токенов
/// </summary>
public sealed class SessionRepository(IDbConnection connection) : ISessionRepository
{
    private static readonly string InsertRefreshSql = $"""
        INSERT INTO {EntityMapper.TbName<RefreshTokenDal>()}({EntityMapper.ColName<RefreshTokenDal>(x => x.Id)},{EntityMapper.ColName<RefreshTokenDal>(x => x.SessionId)},{EntityMapper.ColName<RefreshTokenDal>(x => x.TokenHash)},{EntityMapper.ColName<RefreshTokenDal>(x => x.CreatedAt)},{EntityMapper.ColName<RefreshTokenDal>(x => x.ExpiresAt)},{EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)})
        VALUES(@Id,@SessionId,@TokenHash,@CreatedAt,@ExpiresAt,@RevokedAt)
        """;

    /// <inheritdoc/>
    public async Task CreateAsync(SessionDal session, RefreshTokenDal token)
    {
        using var transaction = connection.BeginTransaction();
        await connection.ExecuteAsync($"""
            INSERT INTO {EntityMapper.TbName<SessionDal>()}({EntityMapper.ColName<SessionDal>(x => x.Id)},{EntityMapper.ColName<SessionDal>(x => x.UserId)},{EntityMapper.ColName<SessionDal>(x => x.CreatedAt)},{EntityMapper.ColName<SessionDal>(x => x.UpdatedAt)},{EntityMapper.ColName<SessionDal>(x => x.ExpiresAt)},{EntityMapper.ColName<SessionDal>(x => x.RevokedAt)},{EntityMapper.ColName<SessionDal>(x => x.RevokeReason)},{EntityMapper.ColName<SessionDal>(x => x.DeviceName)})
            VALUES(@Id,@UserId,@CreatedAt,@UpdatedAt,@ExpiresAt,@RevokedAt,@RevokeReason,@DeviceName)
            """, session, transaction);
        await connection.ExecuteAsync(InsertRefreshSql, token, transaction);
        transaction.Commit();
    }

    /// <inheritdoc/>
    public Task<SessionDal?> GetAsync(Guid sessionId) =>
        connection.QuerySingleOrDefaultAsync<SessionDal>($"SELECT * FROM {EntityMapper.TbName<SessionDal>()} WHERE {EntityMapper.ColName<SessionDal>(x => x.Id)}=@sessionId", new { sessionId });

    /// <inheritdoc/>
    public Task<RefreshTokenDal?> GetRefreshAsync(string hash) =>
        connection.QuerySingleOrDefaultAsync<RefreshTokenDal>($"SELECT * FROM {EntityMapper.TbName<RefreshTokenDal>()} WHERE {EntityMapper.ColName<RefreshTokenDal>(x => x.TokenHash)}=@hash", new { hash });

    /// <inheritdoc/>
    public async Task<bool> RotateAsync(Guid tokenId, RefreshTokenDal next, DateTime now)
    {
        using var transaction = connection.BeginTransaction();
        var session = await connection.QuerySingleOrDefaultAsync<SessionDal>(
            $"SELECT * FROM {EntityMapper.TbName<SessionDal>()} WHERE {EntityMapper.ColName<SessionDal>(x => x.Id)}=@SessionId FOR UPDATE", new { next.SessionId }, transaction);
        if (session is null || session.RevokedAt is not null || session.ExpiresAt <= now)
            return false;

        var affected = await connection.ExecuteAsync($"""
            UPDATE {EntityMapper.TbName<RefreshTokenDal>()} SET {EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)}=@now
            WHERE {EntityMapper.ColName<RefreshTokenDal>(x => x.Id)}=@tokenId AND {EntityMapper.ColName<RefreshTokenDal>(x => x.SessionId)}=@SessionId AND {EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)} IS NULL AND {EntityMapper.ColName<RefreshTokenDal>(x => x.ExpiresAt)}>@now
            """, new { tokenId, next.SessionId, now }, transaction);
        if (affected != 1)
        {
            await connection.ExecuteAsync($"""
                UPDATE {EntityMapper.TbName<SessionDal>()} SET {EntityMapper.ColName<SessionDal>(x => x.RevokedAt)}=@now,{EntityMapper.ColName<SessionDal>(x => x.UpdatedAt)}=@now,{EntityMapper.ColName<SessionDal>(x => x.RevokeReason)}=@reason WHERE {EntityMapper.ColName<SessionDal>(x => x.Id)}=@SessionId;
                UPDATE {EntityMapper.TbName<RefreshTokenDal>()} SET {EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)}=@now WHERE {EntityMapper.ColName<RefreshTokenDal>(x => x.SessionId)}=@SessionId AND {EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)} IS NULL;
                """, new { next.SessionId, now, reason = SessionRevokeReasons.RefreshReuse }, transaction);
            transaction.Commit();
            return false;
        }

        await connection.ExecuteAsync(InsertRefreshSql, next, transaction);
        await connection.ExecuteAsync($"UPDATE {EntityMapper.TbName<SessionDal>()} SET {EntityMapper.ColName<SessionDal>(x => x.UpdatedAt)}=@now WHERE {EntityMapper.ColName<SessionDal>(x => x.Id)}=@SessionId", new { next.SessionId, now }, transaction);
        transaction.Commit();
        return true;
    }

    /// <inheritdoc/>
    public async Task RevokeAsync(Guid sessionId, DateTime now, string reason)
    {
        using var transaction = connection.BeginTransaction();
        await connection.ExecuteAsync($"""
            UPDATE {EntityMapper.TbName<SessionDal>()} SET {EntityMapper.ColName<SessionDal>(x => x.RevokedAt)}=@now,{EntityMapper.ColName<SessionDal>(x => x.RevokeReason)}=@reason,{EntityMapper.ColName<SessionDal>(x => x.UpdatedAt)}=@now WHERE {EntityMapper.ColName<SessionDal>(x => x.Id)}=@sessionId AND {EntityMapper.ColName<SessionDal>(x => x.RevokedAt)} IS NULL;
            UPDATE {EntityMapper.TbName<RefreshTokenDal>()} SET {EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)}=@now WHERE {EntityMapper.ColName<RefreshTokenDal>(x => x.SessionId)}=@sessionId AND {EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)} IS NULL;
            """, new { sessionId, now, reason }, transaction);
        transaction.Commit();
    }

    /// <inheritdoc/>
    public async Task RevokeUserAsync(Guid userId, DateTime now, string reason)
    {
        using var transaction = connection.BeginTransaction();
        await connection.ExecuteAsync($"""
            UPDATE {EntityMapper.TbName<SessionDal>()} SET {EntityMapper.ColName<SessionDal>(x => x.RevokedAt)}=@now,{EntityMapper.ColName<SessionDal>(x => x.RevokeReason)}=@reason,{EntityMapper.ColName<SessionDal>(x => x.UpdatedAt)}=@now WHERE {EntityMapper.ColName<SessionDal>(x => x.UserId)}=@userId AND {EntityMapper.ColName<SessionDal>(x => x.RevokedAt)} IS NULL;
            UPDATE {EntityMapper.TbName<RefreshTokenDal>()} SET {EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)}=@now WHERE {EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)} IS NULL
            AND {EntityMapper.ColName<RefreshTokenDal>(x => x.SessionId)} IN (SELECT {EntityMapper.ColName<RefreshTokenDal>(x => x.Id)} FROM {EntityMapper.TbName<SessionDal>()} WHERE {EntityMapper.ColName<UserProfileDal>(x => x.UserId)}=@userId);
            """, new { userId, now, reason }, transaction);
        transaction.Commit();
    }
}
