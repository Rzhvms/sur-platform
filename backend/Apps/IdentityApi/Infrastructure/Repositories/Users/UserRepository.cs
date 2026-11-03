using Core.Database.DapperExtensions.EntityMapper;
using System.Data;
using Application.Constants;
using Application.Models;
using Application.Ports.Repositories;
using Core.Abstractions.Exceptions;
using Dapper;
using Domain.DalModels;
using Serilog;

namespace Infrastructure.Repositories.Users;

/// <inheritdoc/>
internal sealed class UserRepository : IUserRepository
{
    private readonly IDbConnection _dbConnection;
    private readonly ILogger _logger;
    
    ///
    public UserRepository(IDbConnection dbConnection, ILogger logger)
    {
        _dbConnection = dbConnection;
        _logger = logger.ForContext<UserRepository>();
    }
    
    /// <inheritdoc/>
    public async Task<bool> ExistsByEmailAsync(string normalizedEmail)
    {
        return await _dbConnection.QuerySingleAsync<bool>(
            $"SELECT EXISTS(SELECT 1 FROM {EntityMapper.TbName<UserProfileDal>()} WHERE {EntityMapper.ColName<UserProfileDal>(x => x.NormalizedEmail)} = @normalizedEmail)",
            new { normalizedEmail });
    }

    /// <inheritdoc/>
    public async Task<bool> IsExistAsync(Guid userId)
    {
        var query = $"""
                     SELECT EXISTS(
                        SELECT 1 FROM {EntityMapper.TbName<UserCredentialDal>()}
                        WHERE {EntityMapper.ColName<UserCredentialDal>(x => x.UserId)} = {EntityMapper.ParameterPrefix}{nameof(userId)})
                     )}
                     """;
        
        var result = await _dbConnection.QuerySingleAsync<bool>(query, new { userId });
        return result;
    }

    /// <inheritdoc/>
    public async Task<UserLoginData?> GetLoginDataAsync(string normalizedEmail)
    {
        return await _dbConnection.QuerySingleOrDefaultAsync<UserLoginData>($"""
                                                                             SELECT u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)},
                                                                                 c.{EntityMapper.ColName<UserCredentialDal>(x => x.Password)},
                                                                                 u.{EntityMapper.ColName<UserProfileDal>(x => x.IsActive)},
                                                                                 u.{EntityMapper.ColName<UserProfileDal>(x => x.LockoutUntil)}
                                                                             FROM {EntityMapper.TbName<UserProfileDal>()} u
                                                                             JOIN {EntityMapper.TbName<UserCredentialDal>()} c
                                                                                 ON c.{EntityMapper.ColName<UserCredentialDal>(x => x.UserId)} = u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)}
                                                                             WHERE u.{EntityMapper.ColName<UserProfileDal>(x => x.NormalizedEmail)} = @normalizedEmail
                                                                             """, new { normalizedEmail });
    }

    /// <inheritdoc/>
    public async Task<UserProfileDal?> GetUserByEmailAsync(string email)
    {
        return await _dbConnection.QuerySingleOrDefaultAsync<UserProfileDal>(
            $"SELECT * FROM {EntityMapper.TbName<UserProfileDal>()} WHERE {EntityMapper.ColName<UserProfileDal>(x => x.NormalizedEmail)} = @email", new { email });
    }

    /// <inheritdoc/>
    public async Task<UserProfileDal> GetUserByUserIdAsync(Guid userId)
    {
        var searchQuery = $"""
                           SELECT * FROM {EntityMapper.TbName<UserProfileDal>()}
                           WHERE {EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = {EntityMapper.ParameterPrefix}{nameof(userId)}
                           """;
        
        var user = await _dbConnection.QuerySingleOrDefaultAsync<UserProfileDal>(searchQuery, new { userId });

        if (user is null)
        {
            throw new EntityNotFoundException($"Пользователь {userId} не найден");
        }
        
        return user;
    }

    /// <inheritdoc/>
    public async Task<UserCredentialDal> GetCredentialAsync(Guid userId)
    {
        var searchQuery = $"""
                           SELECT * FROM {EntityMapper.TbName<UserCredentialDal>()}
                           WHERE {EntityMapper.ColName<UserCredentialDal>(x => x.UserId)} = {EntityMapper.ParameterPrefix}{nameof(userId)}
                           """;
        
        var result = await _dbConnection.QuerySingleOrDefaultAsync<UserCredentialDal>(searchQuery, new { userId });

        if (result is null)
        {
            throw new EntityNotFoundException($"Данные пользователя {userId} не найдены");
        }
        
        return result;
    }

    /// <inheritdoc/>
    public async Task CreateUserAsync(UserProfileDal profile, UserCredentialDal credential, Guid roleId)
    {
        using var transaction = _dbConnection.BeginTransaction();

        await _dbConnection.ExecuteAsync($"""
                                          INSERT INTO {EntityMapper.TbName<UserProfileDal>()}({EntityMapper.ColName<UserProfileDal>(x => x.Id)},{EntityMapper.ColName<UserProfileDal>(x => x.UserId)},{EntityMapper.ColName<UserProfileDal>(x => x.FirstName)},{EntityMapper.ColName<UserProfileDal>(x => x.LastName)},{EntityMapper.ColName<UserProfileDal>(x => x.Patronymic)},{EntityMapper.ColName<UserProfileDal>(x => x.Phone)},{EntityMapper.ColName<UserProfileDal>(x => x.Email)},{EntityMapper.ColName<UserProfileDal>(x => x.NormalizedEmail)},
                                              {EntityMapper.ColName<UserProfileDal>(x => x.IsActive)},{EntityMapper.ColName<UserProfileDal>(x => x.IsEmailConfirmed)},{EntityMapper.ColName<UserProfileDal>(x => x.IsPhoneConfirmed)},{EntityMapper.ColName<UserProfileDal>(x => x.LockoutUntil)},{EntityMapper.ColName<UserProfileDal>(x => x.CreatedAt)},{EntityMapper.ColName<UserProfileDal>(x => x.UpdatedAt)})
                                          VALUES(@Id,@UserId,@FirstName,@LastName,@Patronymic,@Phone,@Email,@NormalizedEmail,
                                              @IsActive,@IsEmailConfirmed,@IsPhoneConfirmed,@LockoutUntil,@CreatedAt,@UpdatedAt)
                                          """, profile, transaction);

        await _dbConnection.ExecuteAsync($"""
                                          INSERT INTO {EntityMapper.TbName<UserCredentialDal>()}({EntityMapper.ColName<UserCredentialDal>(x => x.Id)},{EntityMapper.ColName<UserCredentialDal>(x => x.UserId)},{EntityMapper.ColName<UserCredentialDal>(x => x.Password)},{EntityMapper.ColName<UserCredentialDal>(x => x.PasswordChangedAt)})
                                          VALUES(@Id,@UserId,@Password,@PasswordChangedAt)
                                          """, credential, transaction);

        await _dbConnection.ExecuteAsync(
            $"INSERT INTO {EntityMapper.TbName<UserRoleDal>()}({EntityMapper.ColName<UserRoleDal>(x => x.UserId)},{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}) VALUES(@UserId,@roleId)",
            new { profile.UserId, roleId }, transaction);

        transaction.Commit();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<UserProfileDal>> GetUserProfilesAsync()
    {
        var searchQuery = $"""
                           SELECT * FROM {EntityMapper.TbName<UserProfileDal>()}
                           ORDER BY {EntityMapper.ColName<UserProfileDal>(x => x.CreatedAt)},
                                    {EntityMapper.ColName<UserProfileDal>(x => x.UserId)}
                           """;

        var result = await _dbConnection.QueryAsync<UserProfileDal>(searchQuery);
        return result;
    }

    /// <inheritdoc/>
    public async Task ChangeUserPasswordAsync(Guid userId, string password, DateTime changedAt)
    {
        var reason = SessionRevokeReasons.PasswordChanged;
        
        // todo: скорее всего будет медленно и надо будет переделать, но захотел выполнить в одном соединении
        var tripleUpdateQuery = $"""
                                  UPDATE {EntityMapper.TbName<UserCredentialDal>()}
                                  SET {EntityMapper.ColName<UserCredentialDal>(x => x.Password)} = {EntityMapper.ParameterPrefix}{nameof(password)},
                                      {EntityMapper.ColName<UserCredentialDal>(x => x.PasswordChangedAt)} = {EntityMapper.ParameterPrefix}{nameof(changedAt)}
                                  WHERE {EntityMapper.ColName<UserCredentialDal>(x => x.UserId)} = {EntityMapper.ParameterPrefix}{nameof(userId)};

                                  UPDATE {EntityMapper.TbName<SessionDal>()}
                                  SET {EntityMapper.ColName<SessionDal>(x => x.RevokedAt)} = {EntityMapper.ParameterPrefix}{nameof(changedAt)},
                                      {EntityMapper.ColName<SessionDal>(x => x.UpdatedAt)} = {EntityMapper.ParameterPrefix}{nameof(changedAt)},
                                      {EntityMapper.ColName<SessionDal>(x => x.RevokeReason)} = {EntityMapper.ParameterPrefix}{nameof(reason)}
                                  WHERE {EntityMapper.ColName<SessionDal>(x => x.UserId)} = {EntityMapper.ParameterPrefix}{nameof(userId)}
                                  AND {EntityMapper.ColName<SessionDal>(x => x.RevokedAt)} IS NULL;
                                  
                                  UPDATE {EntityMapper.TbName<RefreshTokenDal>()}
                                  SET {EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)} = {EntityMapper.ParameterPrefix}{nameof(changedAt)}
                                  WHERE {EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)} IS NULL 
                                  AND {EntityMapper.ColName<RefreshTokenDal>(x => x.SessionId)} IN (
                                      SELECT {EntityMapper.ColName<RefreshTokenDal>(x => x.Id)}
                                      FROM {EntityMapper.TbName<SessionDal>()}
                                      WHERE {EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = {EntityMapper.ParameterPrefix}{nameof(userId)});
                                  """;
        
        using var transaction = _dbConnection.BeginTransaction();

        await _dbConnection.ExecuteAsync(tripleUpdateQuery, new { userId, password, changedAt, reason }, transaction);
        
        transaction.Commit();
    }

    /// <inheritdoc/>
    public async Task DeleteUserByIdAsync(Guid userId)
    {
        var query = $"""
                     DELETE FROM {EntityMapper.TbName<UserProfileDal>()}
                     WHERE {EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = {EntityMapper.ParameterPrefix}{nameof(userId)}
                     """;
        
        var result = await _dbConnection.ExecuteAsync(query);

        if (result == 0)
        {
            _logger.Error("Пользователь с идентификатором {UserId} не удален", userId);
        }
    }
}