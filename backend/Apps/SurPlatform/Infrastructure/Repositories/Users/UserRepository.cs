using Infrastructure.Constants;
using Core.Database.DapperExtensions.EntityMapper;
using System.Data;
using Application.Constants;
using Application.Models;
using Application.Ports.Repositories;
using Core.Abstractions.Exceptions;
using Dapper;
using Domain.DalModels;

namespace Infrastructure.Repositories.Users;

/// <inheritdoc/>
internal sealed class UserRepository : IUserRepository
{
    /// <summary>
    /// Missing user format
    /// </summary>
    private const string MissingUserFormat = "Данные пользователя {0} не найдены";

    /// <summary>
    /// Подключение выбранной клиентской базы
    /// </summary>
    private readonly IDbConnection _dbConnection;
    
    /// <summary>
    /// Использует рабочее подключение текущего клиента
    /// </summary>
    /// <param name="dbConnection">Подключение Identity</param>
    public UserRepository(IDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    /// <inheritdoc/>
    public async Task<UserLoginData?> GetLoginDataAsync(string normalizedEmail)
    {
        var selectQuery = $"""
            SELECT u.{EntityMapper.ColName<UserProfileDal>(x => x.AccessVersion)}, u.{EntityMapper.ColName<UserProfileDal>(x => x.MustChangePassword)}, u.{EntityMapper.ColName<UserProfileDal>(x => x.TemporaryPasswordExpiresAt)},
                u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)},
                c.{EntityMapper.ColName<UserCredentialDal>(x => x.Password)},
                u.{EntityMapper.ColName<UserProfileDal>(x => x.IsActive)},
                u.{EntityMapper.ColName<UserProfileDal>(x => x.LockoutUntil)}
            FROM {EntityMapper.TbName<UserProfileDal>()} u
            JOIN {EntityMapper.TbName<UserCredentialDal>()} c
                ON c.{EntityMapper.ColName<UserCredentialDal>(x => x.UserId)} = u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)}
            WHERE u.{EntityMapper.ColName<UserProfileDal>(x => x.NormalizedEmail)} = {EntityMapper.ParameterName(SqlParameterNames.NormalizedEmail)}
            """;
        return await _dbConnection.QuerySingleOrDefaultAsync<UserLoginData>(selectQuery, new { normalizedEmail });
    }

    /// <inheritdoc/>
    public async Task<UserCredentialDal> GetCredentialAsync(Guid userId)
    {
        var searchQuery = $"""
            SELECT * FROM {EntityMapper.TbName<UserCredentialDal>()}
            WHERE {EntityMapper.ColName<UserCredentialDal>(x => x.UserId)} = {EntityMapper.ParameterName(nameof(userId))}
            """;

        var result = await _dbConnection.QuerySingleOrDefaultAsync<UserCredentialDal>(searchQuery, new { userId });

        if (result is null)
        {
            throw new EntityNotFoundException(string.Format(System.Globalization.CultureInfo.InvariantCulture, MissingUserFormat, userId));
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<bool> ChangeUserPasswordAsync(Guid userId, string password, DateTime changedAt, string expectedHash)
    {
        using var transaction = _dbConnection.BeginTransaction();
        var updateUserProfileQuery = $"""
            UPDATE {EntityMapper.TbName<UserProfileDal>()} SET {EntityMapper.ColName<UserProfileDal>(x => x.MustChangePassword)} = false, {EntityMapper.ColName<UserProfileDal>(x => x.TemporaryPasswordExpiresAt)} = NULL,
                {EntityMapper.ColName<UserProfileDal>(x => x.Version)} = gen_random_uuid(), {EntityMapper.ColName<UserProfileDal>(x => x.AccessVersion)} = {EntityMapper.ColName<UserProfileDal>(x => x.AccessVersion)} + 1, {EntityMapper.ColName<UserProfileDal>(x => x.UpdatedAt)} = {EntityMapper.ParameterName(SqlParameterNames.ChangedAt)},
                {EntityMapper.ColName<UserProfileDal>(x => x.UpdatedBy)} = {EntityMapper.ParameterName(SqlParameterNames.UserId)}
            WHERE {EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = {EntityMapper.ParameterName(SqlParameterNames.UserId)} AND {EntityMapper.ColName<UserProfileDal>(x => x.IsActive)} AND
                (NOT {EntityMapper.ColName<UserProfileDal>(x => x.MustChangePassword)} OR {EntityMapper.ColName<UserProfileDal>(x => x.TemporaryPasswordExpiresAt)} > {EntityMapper.ParameterName(SqlParameterNames.ChangedAt)})
            """;
        var updated = await _dbConnection.ExecuteAsync(updateUserProfileQuery, new { userId, changedAt }, transaction);
        if (updated != 1)
        {
            return false;
        }

        var updateUserCredentialQuery = $"""
            UPDATE {EntityMapper.TbName<UserCredentialDal>()} SET {EntityMapper.ColName<UserCredentialDal>(x => x.Password)} = {EntityMapper.ParameterName(SqlParameterNames.Password)}, {EntityMapper.ColName<UserCredentialDal>(x => x.PasswordChangedAt)} = {EntityMapper.ParameterName(SqlParameterNames.ChangedAt)}
            WHERE {EntityMapper.ColName<UserCredentialDal>(x => x.UserId)} = {EntityMapper.ParameterName(SqlParameterNames.UserId)} AND {EntityMapper.ColName<UserCredentialDal>(x => x.Password)} = {EntityMapper.ParameterName(SqlParameterNames.ExpectedHash)}
            """;
        
        var credentialUpdated = await _dbConnection.ExecuteAsync(updateUserCredentialQuery, new { userId, password, changedAt, expectedHash }, transaction);
        if (credentialUpdated != 1)
        {
            return false;
        }

        var updateSessionQuery = $"""
            UPDATE {EntityMapper.TbName<SessionDal>()} SET {EntityMapper.ColName<SessionDal>(x => x.RevokedAt)} = {EntityMapper.ParameterName(SqlParameterNames.ChangedAt)}, {EntityMapper.ColName<SessionDal>(x => x.UpdatedAt)} = {EntityMapper.ParameterName(SqlParameterNames.ChangedAt)},
                            {EntityMapper.ColName<SessionDal>(x => x.RevokeReason)} = {EntityMapper.ParameterName(SqlParameterNames.Reason)}, {EntityMapper.ColName<SessionDal>(x => x.Version)} = gen_random_uuid()
                        WHERE {EntityMapper.ColName<SessionDal>(x => x.UserId)} = {EntityMapper.ParameterName(SqlParameterNames.UserId)} AND {EntityMapper.ColName<SessionDal>(x => x.RevokedAt)} IS NULL
            """;
        await _dbConnection.ExecuteAsync(updateSessionQuery, new { userId, changedAt, reason = SessionRevokeReasons.PasswordChanged }, transaction);
        var updateRefreshTokenQuery2 = $"""
            UPDATE {EntityMapper.TbName<RefreshTokenDal>()} t SET {EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)} = {EntityMapper.ParameterName(SqlParameterNames.ChangedAt)} WHERE t.{EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)} IS NULL AND EXISTS (
                            SELECT FROM {EntityMapper.TbName<SessionDal>()} s WHERE s.{EntityMapper.ColName<SessionDal>(x => x.Id)} = t.{EntityMapper.ColName<RefreshTokenDal>(x => x.SessionId)} AND s.{EntityMapper.ColName<SessionDal>(x => x.UserId)} = {EntityMapper.ParameterName(SqlParameterNames.UserId)})
            """;
        await _dbConnection.ExecuteAsync(updateRefreshTokenQuery2, new { userId, changedAt, reason = SessionRevokeReasons.PasswordChanged }, transaction);
        transaction.Commit();
        return true;
    }
}
