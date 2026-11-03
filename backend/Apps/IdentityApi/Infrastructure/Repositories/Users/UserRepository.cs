using Core.Database.DapperExtensions.EntityMapper;
using System.Data;
using Application.Constants;
using Application.Ports.Repositories;
using Dapper;
using Domain.DalModels;

namespace Infrastructure.Repositories.Users;

/// <inheritdoc/>
public class UserRepository(IDbConnection dbConnection) : IUserRepository
{
    /// <inheritdoc/>
    public async Task<UserProfileDal?> GetUserByEmailAsync(string email)
    {
        return await dbConnection.QuerySingleOrDefaultAsync<UserProfileDal>(
            $"SELECT * FROM {EntityMapper.TbName<UserProfileDal>()} WHERE {EntityMapper.ColName<UserProfileDal>(x => x.NormalizedEmail)} = @email", new { email });
    }

    /// <inheritdoc/>
    public async Task<UserProfileDal?> GetUserByUserIdAsync(Guid userId)
    {
        return await dbConnection.QuerySingleOrDefaultAsync<UserProfileDal>(
            $"SELECT * FROM {EntityMapper.TbName<UserProfileDal>()} WHERE {EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = @userId", new { userId });
    }

    /// <inheritdoc/>
    public async Task<UserCredentialDal?> GetCredentialAsync(Guid userId)
    {
        return await dbConnection.QuerySingleOrDefaultAsync<UserCredentialDal>(
            $"SELECT * FROM {EntityMapper.TbName<UserCredentialDal>()} WHERE {EntityMapper.ColName<UserCredentialDal>(x => x.UserId)} = @userId", new { userId });
    }

    /// <inheritdoc/>
    public async Task CreateUserAsync(UserProfileDal profile, UserCredentialDal credential, Guid roleId)
    {
        using var transaction = dbConnection.BeginTransaction();

        await dbConnection.ExecuteAsync($"""
            INSERT INTO {EntityMapper.TbName<UserProfileDal>()}({EntityMapper.ColName<UserProfileDal>(x => x.Id)},{EntityMapper.ColName<UserProfileDal>(x => x.UserId)},{EntityMapper.ColName<UserProfileDal>(x => x.FirstName)},{EntityMapper.ColName<UserProfileDal>(x => x.LastName)},{EntityMapper.ColName<UserProfileDal>(x => x.Patronymic)},{EntityMapper.ColName<UserProfileDal>(x => x.Phone)},{EntityMapper.ColName<UserProfileDal>(x => x.Email)},{EntityMapper.ColName<UserProfileDal>(x => x.NormalizedEmail)},
                {EntityMapper.ColName<UserProfileDal>(x => x.IsActive)},{EntityMapper.ColName<UserProfileDal>(x => x.IsEmailConfirmed)},{EntityMapper.ColName<UserProfileDal>(x => x.IsPhoneConfirmed)},{EntityMapper.ColName<UserProfileDal>(x => x.LockoutUntil)},{EntityMapper.ColName<UserProfileDal>(x => x.CreatedAt)},{EntityMapper.ColName<UserProfileDal>(x => x.UpdatedAt)})
            VALUES(@Id,@UserId,@FirstName,@LastName,@Patronymic,@Phone,@Email,@NormalizedEmail,
                @IsActive,@IsEmailConfirmed,@IsPhoneConfirmed,@LockoutUntil,@CreatedAt,@UpdatedAt)
            """, profile, transaction);

        await dbConnection.ExecuteAsync($"""
            INSERT INTO {EntityMapper.TbName<UserCredentialDal>()}({EntityMapper.ColName<UserCredentialDal>(x => x.Id)},{EntityMapper.ColName<UserCredentialDal>(x => x.UserId)},{EntityMapper.ColName<UserCredentialDal>(x => x.Password)},{EntityMapper.ColName<UserCredentialDal>(x => x.PasswordChangedAt)})
            VALUES(@Id,@UserId,@Password,@PasswordChangedAt)
            """, credential, transaction);

        await dbConnection.ExecuteAsync(
            $"INSERT INTO {EntityMapper.TbName<UserRoleDal>()}({EntityMapper.ColName<UserRoleDal>(x => x.UserId)},{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}) VALUES(@UserId,@roleId)",
            new { profile.UserId, roleId }, transaction);

        transaction.Commit();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<UserProfileDal>> GetAllUsersAsync()
    {
        return await dbConnection.QueryAsync<UserProfileDal>(
            $"SELECT * FROM {EntityMapper.TbName<UserProfileDal>()} ORDER BY {EntityMapper.ColName<UserProfileDal>(x => x.CreatedAt)},{EntityMapper.ColName<UserProfileDal>(x => x.UserId)}");
    }

    /// <inheritdoc/>
    public async Task ChangeUserPasswordAsync(Guid userId, string password, DateTime changedAt)
    {
        using var transaction = dbConnection.BeginTransaction();

        await dbConnection.ExecuteAsync($"""
            UPDATE {EntityMapper.TbName<UserCredentialDal>()} SET {EntityMapper.ColName<UserCredentialDal>(x => x.Password)} = @password, {EntityMapper.ColName<UserCredentialDal>(x => x.PasswordChangedAt)} = @changedAt
            WHERE {EntityMapper.ColName<UserCredentialDal>(x => x.UserId)} = @userId;
            UPDATE {EntityMapper.TbName<SessionDal>()} SET {EntityMapper.ColName<SessionDal>(x => x.RevokedAt)} = @changedAt, {EntityMapper.ColName<SessionDal>(x => x.UpdatedAt)} = @changedAt, {EntityMapper.ColName<SessionDal>(x => x.RevokeReason)} = @reason
            WHERE {EntityMapper.ColName<SessionDal>(x => x.UserId)} = @userId AND {EntityMapper.ColName<SessionDal>(x => x.RevokedAt)} IS NULL;
            UPDATE {EntityMapper.TbName<RefreshTokenDal>()} SET {EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)} = @changedAt
            WHERE {EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)} IS NULL AND {EntityMapper.ColName<RefreshTokenDal>(x => x.SessionId)} IN (SELECT {EntityMapper.ColName<RefreshTokenDal>(x => x.Id)} FROM {EntityMapper.TbName<SessionDal>()} WHERE {EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = @userId);
            """, new { userId, password, changedAt, reason = SessionRevokeReasons.PasswordChanged }, transaction);

        transaction.Commit();
    }

    /// <inheritdoc/>
    public async Task DeleteUserByIdAsync(Guid userId)
    {
        await dbConnection.ExecuteAsync(
            $"DELETE FROM {EntityMapper.TbName<UserProfileDal>()} WHERE {EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = @userId", new { userId });
    }
}
