using Infrastructure.Constants;
using Domain.DalModels;
using Core.Database.DapperExtensions.EntityMapper;
using System.Data;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.Ports.Repositories;
using Application.UseCases.Users.Dto.Request;
using Dapper;
using Infrastructure.Repositories.Organization.Interfaces;
using Application.UseCases.Organization.Dto.Request;

namespace Infrastructure.Repositories.Identity;

/// <inheritdoc/>
/// <param name="connection">Рабочее подключение выбранной клиентской базы</param>
/// <param name="organization">Организационные операции в той же транзакции</param>
internal sealed class IdentityAdministrationRepository(IDbConnection connection, IOrganizationIdentityRepository organization) : IIdentityAdministrationRepository
{
    /// <summary>
    /// Срок, после которого неподтверждённая доставка считается потерянной
    /// </summary>
    private const string InvitationTimeoutSql = "interval '2 minutes'";

    /// <summary>
    /// Выборка административной карточки без секретов
    /// </summary>
    /// <returns>Запрос без дополнительных фильтров</returns>
    private string BuildUserQuery()
    {
        var query = $"""
            SELECT u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} AS {EntityMapper.ColName<IdentityUser>(x => x.Id)}, u.{EntityMapper.ColName<UserProfileDal>(x => x.Email)}, u.{EntityMapper.ColName<UserProfileDal>(x => x.FullName)}, u.{EntityMapper.ColName<UserProfileDal>(x => x.IsActive)}, u.{EntityMapper.ColName<UserProfileDal>(x => x.Version)},
                CASE WHEN u.{EntityMapper.ColName<UserProfileDal>(x => x.InvitationStatus)} = '{IdentityAccounts.InvitationPending}' AND u.{EntityMapper.ColName<UserProfileDal>(x => x.InvitationStartedAt)} <= now() - {InvitationTimeoutSql}
                    THEN '{IdentityAccounts.InvitationFailed}' ELSE u.{EntityMapper.ColName<UserProfileDal>(x => x.InvitationStatus)} END AS {EntityMapper.ColName<IdentityUser>(x => x.InvitationStatus)}, u.{EntityMapper.ColName<UserProfileDal>(x => x.CreatedAt)}, u.{EntityMapper.ColName<UserProfileDal>(x => x.UpdatedAt)}, u.{EntityMapper.ColName<UserProfileDal>(x => x.UpdatedBy)}, u.{EntityMapper.ColName<UserProfileDal>(x => x.FirstName)}, u.{EntityMapper.ColName<UserProfileDal>(x => x.LastName)}, u.{EntityMapper.ColName<UserProfileDal>(x => x.Patronymic)},
                ARRAY(SELECT r.{EntityMapper.ColName<RoleDal>(x => x.Id)} FROM {EntityMapper.TbName<UserRoleDal>()} a JOIN {EntityMapper.TbName<RoleDal>()} r ON r.{EntityMapper.ColName<RoleDal>(x => x.Id)} = a.{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}
                    WHERE a.{EntityMapper.ColName<UserRoleDal>(x => x.UserId)} = u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} AND a.{EntityMapper.ColName<UserRoleDal>(x => x.IsActive)} AND r.{EntityMapper.ColName<RoleDal>(x => x.Code)} IS NOT NULL ORDER BY r.{EntityMapper.ColName<RoleDal>(x => x.Name)}) AS {EntityMapper.ColName<IdentityUser>(x => x.RoleIds)},
                ARRAY(SELECT r.{EntityMapper.ColName<RoleDal>(x => x.Name)} FROM {EntityMapper.TbName<UserRoleDal>()} a JOIN {EntityMapper.TbName<RoleDal>()} r ON r.{EntityMapper.ColName<RoleDal>(x => x.Id)} = a.{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}
                    WHERE a.{EntityMapper.ColName<UserRoleDal>(x => x.UserId)} = u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} AND a.{EntityMapper.ColName<UserRoleDal>(x => x.IsActive)} AND r.{EntityMapper.ColName<RoleDal>(x => x.Code)} IS NOT NULL ORDER BY r.{EntityMapper.ColName<RoleDal>(x => x.Name)}) AS {EntityMapper.ColName<IdentityUser>(x => x.Roles)},
                ARRAY({organization.BuildResponsibilityLabelsQuery($"u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)}")})
                    AS {EntityMapper.ColName<IdentityUser>(x => x.Responsibilities)}
            FROM {EntityMapper.TbName<UserProfileDal>()} u WHERE u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} <> {EntityMapper.ParameterName(SqlParameterNames.SystemUserId)}
            """;
        return query;
    }

    /// <summary>
    /// Выборка системной роли с назначенными правами
    /// </summary>
    /// <returns>Запрос без дополнительных фильтров</returns>
    private static string BuildRoleQuery()
    {
        var query = $"""
            SELECT r.*, ARRAY(SELECT rr.{EntityMapper.ColName<RoleRightDal>(x => x.RightId)} FROM {EntityMapper.TbName<RoleRightDal>()} rr WHERE rr.{EntityMapper.ColName<RoleRightDal>(x => x.RoleId)} = r.{EntityMapper.ColName<RoleDal>(x => x.Id)}
                ORDER BY rr.{EntityMapper.ColName<RoleRightDal>(x => x.RightId)}) AS {EntityMapper.ColName<IdentityRole>(x => x.RightIds)}
            FROM {EntityMapper.TbName<RoleDal>()} r WHERE r.{EntityMapper.ColName<RoleDal>(x => x.Code)} IS NOT NULL
            """;
        return query;
    }

    /// <inheritdoc/>
    public async Task<IdentityUser[]> ListAsync(Guid after, int limit)
    {
        var selectQuery = BuildUserQuery() +
            $""" AND u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} > {EntityMapper.ParameterName(SqlParameterNames.After)} ORDER BY u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} LIMIT {EntityMapper.ParameterName(SqlParameterNames.Limit)}""";
        return (await connection.QueryAsync<IdentityUser>(selectQuery,
            new { systemUserId = IdentityAccounts.SystemUserId, after, limit })).ToArray();
    }

    /// <inheritdoc/>
    public Task<IdentityUser?> FindAsync(Guid userId)
    {
        return FindAsync(userId, null);
    }

    /// <inheritdoc/>
    public async Task<IdentityRole[]> RolesAsync()
    {
        var selectCommandQuery = BuildRoleQuery() + $""" ORDER BY r.{EntityMapper.ColName<RoleDal>(x => x.Code)} """;
        return (await connection.QueryAsync<IdentityRole>(selectCommandQuery)).ToArray();
    }

    /// <inheritdoc/>
    public async Task<IdentityRight[]> RightsAsync()
    {
        var selectRightQuery = $"""
            SELECT {EntityMapper.ColName<RightDal>(x => x.Id)}, {EntityMapper.ColName<RightDal>(x => x.Code)}, {EntityMapper.ColName<RightDal>(x => x.Name)} FROM {EntityMapper.TbName<RightDal>()} WHERE {EntityMapper.ColName<RightDal>(x => x.Code)} IS NOT NULL ORDER BY {EntityMapper.ColName<RightDal>(x => x.Code)}
            """;
        return (await connection.QueryAsync<IdentityRight>(selectRightQuery)).ToArray();
    }

    /// <inheritdoc/>
    public Task SynchronizeCatalogAsync(Guid actor, Guid sessionId)
    {
        return AdministrationTransaction.WriteAsync(connection, actor, sessionId, async transaction =>
        {
            await IdentityCatalogWriter.SynchronizeAsync(connection, transaction);
            return true;
        });
    }

    /// <inheritdoc/>
    public Task<IdentityInvitation> CreateAsync(Guid actor, Guid sessionId, CreateIdentityUserRequest request, string passwordHash)
    {
        return AdministrationTransaction.WriteAsync(connection, actor, sessionId, async transaction =>
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
            {
                throw new IdentityCommandException(400, IdentityErrorCodes.FullNameRequired);
            }

            var roles = await RequireRolesAsync(request.RoleIds, transaction);
            return await InsertAsync(request.Email, request.FullName.Trim(), roles, actor, passwordHash, false, transaction, request.Organization);
        });
    }

    /// <inheritdoc/>
    public Task<IdentityUser> UpdateAsync(Guid actor, Guid sessionId, Guid userId, UpdateIdentityUserRequest request)
    {
        return AdministrationTransaction.WriteAsync(connection, actor, sessionId, async transaction =>
        {
            var user = await RequireUserAsync(userId, request.Version, transaction);
            if (user.IsActive)
            {
                if (request.Email is not null || request.FullName is not null && string.IsNullOrWhiteSpace(request.FullName))
                {
                    throw new IdentityCommandException(400, IdentityErrorCodes.ActiveUserFieldsInvalid);
                }

                if (request.RoleIds is not null)
                {
                    var roles = await RequireRolesAsync(request.RoleIds, transaction);
                    await SetRolesAsync(userId, roles, actor, transaction);
                    await organization.RequireValidRolesAsync(userId, null, transaction);
                }
            }
            else if (request.Email is null || request.FullName is not null || request.RoleIds is not null)
            {
                throw new IdentityCommandException(400, IdentityErrorCodes.InactiveUserFieldsInvalid);
            }

            var updateUserProfileQuery = $"""
                UPDATE {EntityMapper.TbName<UserProfileDal>()} SET {EntityMapper.ColName<UserProfileDal>(x => x.FullName)} = COALESCE({EntityMapper.ParameterName(SqlParameterNames.FullName)}, {EntityMapper.ColName<UserProfileDal>(x => x.FullName)}),
                    {EntityMapper.ColName<UserProfileDal>(x => x.Email)} = COALESCE({EntityMapper.ParameterName(SqlParameterNames.Email)}, {EntityMapper.ColName<UserProfileDal>(x => x.Email)}), {EntityMapper.ColName<UserProfileDal>(x => x.NormalizedEmail)} = COALESCE({EntityMapper.ParameterName(SqlParameterNames.NormalizedEmail)}, {EntityMapper.ColName<UserProfileDal>(x => x.NormalizedEmail)}),
                    {EntityMapper.ColName<UserProfileDal>(x => x.UpdatedAt)} = now(), {EntityMapper.ColName<UserProfileDal>(x => x.UpdatedBy)} = {EntityMapper.ParameterName(SqlParameterNames.Actor)}, {EntityMapper.ColName<UserProfileDal>(x => x.Version)} = gen_random_uuid(),
                    {EntityMapper.ColName<UserProfileDal>(x => x.AccessVersion)} = {EntityMapper.ColName<UserProfileDal>(x => x.AccessVersion)} + CASE WHEN {EntityMapper.ParameterName(SqlParameterNames.RolesChanged)} THEN 1 ELSE 0 END
                WHERE {EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = {EntityMapper.ParameterName(SqlParameterNames.UserId)}
                """;
            await connection.ExecuteAsync(updateUserProfileQuery, new { userId, actor, fullName = request.FullName?.Trim(), email = request.Email?.Trim(),
                    normalizedEmail = request.Email?.Trim().ToLowerInvariant(), rolesChanged = request.RoleIds is not null }, transaction);
            if (request.FullName is not null && request.FullName.Trim() != user.FullName)
            {
                await organization.SnapshotPersonNameAsync(userId, actor, transaction);
            }

            await EnsureAdministratorAsync(transaction);
            return await FindAsync(userId, transaction) ?? throw new IdentityCommandException(404, IdentityErrorCodes.UserNotFound);
        });
    }

    /// <inheritdoc/>
    public Task<IdentityRole> SaveRoleAsync(Guid actor, Guid sessionId, Guid roleId, UpdateIdentityRoleRequest request)
    {
        return AdministrationTransaction.WriteAsync(connection, actor, sessionId, async transaction =>
        {
            var role = await FindRoleAsync(roleId, transaction);
            if (role.Version != request.Version)
            {
                throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
            }

            var rightIds = request.RightIds.Distinct().ToArray();
            var admin = Guid.Parse(IdentityRights.Admin);
            if (role.Code == IdentityAccounts.AdminRole &&
                (!request.IsActive || rightIds.Length != 1 || rightIds[0] != admin))
            {
                throw new IdentityCommandException(409, IdentityErrorCodes.AdminRoleProtected);
            }

            var selectRightCommandQuery = $"""
                SELECT count(*) FROM {EntityMapper.TbName<RightDal>()} WHERE {EntityMapper.ColName<RightDal>(x => x.Code)} IS NOT NULL AND {EntityMapper.ColName<RightDal>(x => x.Id)} = ANY({EntityMapper.ParameterName(SqlParameterNames.RightIds)})
                """;
            var valid = await connection.ExecuteScalarAsync<long>(selectRightCommandQuery, new { rightIds }, transaction);
            if (valid != rightIds.Length)
            {
                throw new IdentityCommandException(400, IdentityErrorCodes.UnknownRight);
            }

            var selectUserRoleQuery = $"""
                SELECT EXISTS (
                    SELECT FROM {EntityMapper.TbName<UserRoleDal>()} a JOIN {EntityMapper.TbName<UserProfileDal>()} u ON u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = a.{EntityMapper.ColName<UserRoleDal>(x => x.UserId)}
                    WHERE a.{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)} = {EntityMapper.ParameterName(SqlParameterNames.RoleId)} AND a.{EntityMapper.ColName<UserRoleDal>(x => x.IsActive)} AND u.{EntityMapper.ColName<UserProfileDal>(x => x.IsActive)} AND NOT EXISTS (
                        SELECT FROM {EntityMapper.TbName<UserRoleDal>()} other JOIN {EntityMapper.TbName<RoleDal>()} r ON r.{EntityMapper.ColName<RoleDal>(x => x.Id)} = other.{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}
                        WHERE other.{EntityMapper.ColName<UserRoleDal>(x => x.UserId)} = a.{EntityMapper.ColName<UserRoleDal>(x => x.UserId)} AND other.{EntityMapper.ColName<UserRoleDal>(x => x.IsActive)} AND r.{EntityMapper.ColName<RoleDal>(x => x.IsActive)}
                            AND r.{EntityMapper.ColName<RoleDal>(x => x.Id)} <> {EntityMapper.ParameterName(SqlParameterNames.RoleId)} AND r.{EntityMapper.ColName<RoleDal>(x => x.Code)} IS NOT NULL))
                """;
            if (!request.IsActive && await connection.ExecuteScalarAsync<bool>(selectUserRoleQuery, new { roleId }, transaction))
            {
                throw new IdentityCommandException(409, IdentityErrorCodes.RoleDeactivationBlockedLastRole);
            }

            var updateRoleQuery = $"""
                UPDATE {EntityMapper.TbName<RoleDal>()} SET {EntityMapper.ColName<RoleDal>(x => x.IsActive)} = {EntityMapper.ParameterName(SqlParameterNames.IsActive)}, {EntityMapper.ColName<RoleDal>(x => x.Version)} = gen_random_uuid(), {EntityMapper.ColName<RoleDal>(x => x.UpdatedAt)} = now()
                                        WHERE {EntityMapper.ColName<RoleDal>(x => x.Id)} = {EntityMapper.ParameterName(SqlParameterNames.RoleId)}
                """;
            await connection.ExecuteAsync(updateRoleQuery, new { roleId, isActive = request.IsActive, rightIds }, transaction);
            if (!request.IsActive)
            {
                await organization.RequireValidRolesAsync(null, role.Code, transaction);
            }

            var deletePreviousRightsQuery = $"""
                DELETE FROM {EntityMapper.TbName<RoleRightDal>()} WHERE {EntityMapper.ColName<RoleRightDal>(x => x.RoleId)} = {EntityMapper.ParameterName(SqlParameterNames.RoleId)} AND {EntityMapper.ColName<RoleRightDal>(x => x.RightId)} <> ALL({EntityMapper.ParameterName(SqlParameterNames.RightIds)})
                """;
            await connection.ExecuteAsync(deletePreviousRightsQuery, new { roleId, isActive = request.IsActive, rightIds }, transaction);
            var insertAssignedRightsQuery = $"""
                INSERT INTO {EntityMapper.TbName<RoleRightDal>()} ({EntityMapper.ColName<RoleRightDal>(x => x.Id)}, {EntityMapper.ColName<RoleRightDal>(x => x.RoleId)}, {EntityMapper.ColName<RoleRightDal>(x => x.RightId)})
                                        SELECT gen_random_uuid(), {EntityMapper.ParameterName(SqlParameterNames.RoleId)}, unnest({EntityMapper.ParameterName(SqlParameterNames.RightIds)}) ON CONFLICT ({EntityMapper.ColName<RoleRightDal>(x => x.RoleId)}, {EntityMapper.ColName<RoleRightDal>(x => x.RightId)}) DO NOTHING
                """;
            await connection.ExecuteAsync(insertAssignedRightsQuery, new { roleId, isActive = request.IsActive, rightIds }, transaction);
            var invalidateAccessVersionsQuery = $"""
                UPDATE {EntityMapper.TbName<UserProfileDal>()} u SET {EntityMapper.ColName<UserProfileDal>(x => x.AccessVersion)} = u.{EntityMapper.ColName<UserProfileDal>(x => x.AccessVersion)} + 1
                                        WHERE EXISTS (SELECT FROM {EntityMapper.TbName<UserRoleDal>()} a WHERE a.{EntityMapper.ColName<UserRoleDal>(x => x.UserId)} = u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)}
                                            AND a.{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)} = {EntityMapper.ParameterName(SqlParameterNames.RoleId)} AND a.{EntityMapper.ColName<UserRoleDal>(x => x.IsActive)})
                """;
            await connection.ExecuteAsync(invalidateAccessVersionsQuery, new { roleId, isActive = request.IsActive, rightIds }, transaction);
            await EnsureAdministratorAsync(transaction);
            return await FindRoleAsync(roleId, transaction);
        });
    }

    /// <inheritdoc/>
    public Task<IdentityInvitation> IssueAsync(Guid actor, Guid sessionId, Guid userId,
        ActivateIdentityUserRequest? activation, Guid version, string passwordHash)
    {
        return AdministrationTransaction.WriteAsync(connection, actor, sessionId, async transaction =>
        {
            var user = await RequireUserAsync(userId, version, transaction);
            if (activation is not null)
            {
                if (user.IsActive)
                {
                    throw new IdentityCommandException(409, IdentityErrorCodes.UserAlreadyActive);
                }

                if (activation.RoleIds is not null)
                {
                    var roles = await RequireRolesAsync(activation.RoleIds, transaction);
                    await SetRolesAsync(userId, roles, actor, transaction);
                }
                else
                {
                    var checkActiveRoleQuery = $"""
                        SELECT EXISTS (
                            SELECT FROM {EntityMapper.TbName<UserRoleDal>()} a
                            JOIN {EntityMapper.TbName<RoleDal>()} r
                                ON r.{EntityMapper.ColName<RoleDal>(x => x.Id)} = a.{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}
                            WHERE a.{EntityMapper.ColName<UserRoleDal>(x => x.UserId)} = {EntityMapper.ParameterName(SqlParameterNames.UserId)}
                                AND a.{EntityMapper.ColName<UserRoleDal>(x => x.IsActive)}
                                AND r.{EntityMapper.ColName<RoleDal>(x => x.IsActive)}
                                AND r.{EntityMapper.ColName<RoleDal>(x => x.Code)} IS NOT NULL
                        )
                        """;
                    if (!await connection.ExecuteScalarAsync<bool>(checkActiveRoleQuery, new { userId }, transaction))
                    {
                        throw new IdentityCommandException(409, IdentityErrorCodes.ActiveRoleRequired);
                    }
                }
            }

            if (!user.IsActive && activation is null)
            {
                throw new IdentityCommandException(409, OrganizationErrors.Ineligible);
            }

            return await StartInvitationAsync(user, actor, passwordHash, activation is not null, transaction);
        });
    }

    /// <inheritdoc/>
    public Task<IdentityInvitation?> BootstrapAsync(string email, string fullName, string passwordHash, bool reissue)
    {
        return AdministrationTransaction.WriteAsync<IdentityInvitation?>(connection, IdentityAccounts.SystemUserId, null, async transaction =>
            {
                var selectUserProfileQuery = $"""
                    SELECT {EntityMapper.ColName<UserProfileDal>(x => x.UserId)} FROM {EntityMapper.TbName<UserProfileDal>()} WHERE {EntityMapper.ColName<UserProfileDal>(x => x.IsFirstAdministrator)}
                    """;
                var firstId = await connection.QuerySingleOrDefaultAsync<Guid?>(selectUserProfileQuery, transaction: transaction);
                if (firstId is Guid id)
                {
                    if (!reissue)
                    {
                        return null;
                    }

                    var user = await FindAsync(id, transaction) ?? throw new IdentityCommandException(404, IdentityErrorCodes.UserNotFound);
                    return await StartInvitationAsync(user, IdentityAccounts.SystemUserId, passwordHash, false, transaction);
                }

                if (reissue)
                {
                    throw new IdentityCommandException(404, IdentityErrorCodes.FirstAdminNotFound);
                }

                var selectRoleQuery = $"""
                    SELECT {EntityMapper.ColName<RoleDal>(x => x.Id)} FROM {EntityMapper.TbName<RoleDal>()} WHERE {EntityMapper.ColName<RoleDal>(x => x.Code)} = {EntityMapper.ParameterName(SqlParameterNames.Role)}
                    """;
                var roleId = await connection.QuerySingleAsync<Guid>(selectRoleQuery, new { role = IdentityAccounts.AdminRole }, transaction);
                return await InsertAsync(email, fullName, [roleId], IdentityAccounts.SystemUserId, passwordHash, true, transaction);
            });
    }

    /// <inheritdoc/>
    public Task<bool> IsInvitationCurrentAsync(Guid userId, Guid version)
    {
        var selectUserProfileCommandQuery = $"""
            SELECT EXISTS (SELECT FROM {EntityMapper.TbName<UserProfileDal>()} WHERE {EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = {EntityMapper.ParameterName(SqlParameterNames.UserId)}
                AND {EntityMapper.ColName<UserProfileDal>(x => x.InvitationVersion)} = {EntityMapper.ParameterName(SqlParameterNames.Version)} AND {EntityMapper.ColName<UserProfileDal>(x => x.InvitationStatus)} = '{IdentityAccounts.InvitationPending}'
                AND {EntityMapper.ColName<UserProfileDal>(x => x.InvitationStartedAt)} > now() - {InvitationTimeoutSql})
            """;
        return connection.ExecuteScalarAsync<bool>(selectUserProfileCommandQuery, new { userId, version });
    }

    /// <inheritdoc/>
    public Task FinishInvitationAsync(Guid userId, Guid version, string status)
    {
        var updateUserProfileCommandQuery = $"""
            UPDATE {EntityMapper.TbName<UserProfileDal>()} SET {EntityMapper.ColName<UserProfileDal>(x => x.InvitationStatus)} = {EntityMapper.ParameterName(SqlParameterNames.Status)}, {EntityMapper.ColName<UserProfileDal>(x => x.UpdatedAt)} = now(), {EntityMapper.ColName<UserProfileDal>(x => x.Version)} = gen_random_uuid()
            WHERE {EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = {EntityMapper.ParameterName(SqlParameterNames.UserId)} AND {EntityMapper.ColName<UserProfileDal>(x => x.InvitationVersion)} = {EntityMapper.ParameterName(SqlParameterNames.Version)} AND {EntityMapper.ColName<UserProfileDal>(x => x.InvitationStatus)} = '{IdentityAccounts.InvitationPending}'
                AND {EntityMapper.ColName<UserProfileDal>(x => x.InvitationStartedAt)} > now() - {InvitationTimeoutSql}
            """;
        return connection.ExecuteAsync(updateUserProfileCommandQuery, new { userId, version, status });
    }

    /// <summary>
    /// Проверяет наличие активного администратора после изменения
    /// </summary>
    /// <param name="transaction">Транзакция административной команды</param>
    private async Task EnsureAdministratorAsync(IDbTransaction transaction)
    {
        var checkRemainingAdministratorQuery = $"""
            SELECT EXISTS (SELECT FROM {EntityMapper.TbName<UserProfileDal>()} u JOIN {EntityMapper.TbName<UserRoleDal>()} a ON a.{EntityMapper.ColName<UserRoleDal>(x => x.UserId)} = u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)}
                JOIN {EntityMapper.TbName<RoleDal>()} r ON r.{EntityMapper.ColName<RoleDal>(x => x.Id)} = a.{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)} JOIN {EntityMapper.TbName<RoleRightDal>()} rr ON rr.{EntityMapper.ColName<RoleRightDal>(x => x.RoleId)} = r.{EntityMapper.ColName<RoleDal>(x => x.Id)}
                WHERE u.{EntityMapper.ColName<UserProfileDal>(x => x.IsActive)} AND a.{EntityMapper.ColName<UserRoleDal>(x => x.IsActive)} AND r.{EntityMapper.ColName<RoleDal>(x => x.IsActive)} AND r.{EntityMapper.ColName<RoleDal>(x => x.Code)} IS NOT NULL AND rr.{EntityMapper.ColName<RoleRightDal>(x => x.RightId)} = {EntityMapper.ParameterName(SqlParameterNames.Admin)})
            """;
        if (!await connection.ExecuteScalarAsync<bool>(checkRemainingAdministratorQuery, new { admin = Guid.Parse(IdentityRights.Admin) }, transaction))
        {
            throw new IdentityCommandException(409, IdentityErrorCodes.LastAdministrator);
        }
    }

    /// <summary>
    /// Читает карточку в текущем снимке операции
    /// </summary>
    /// <param name="userId">Идентификатор учётной записи</param>
    /// <param name="transaction">Текущая транзакция, если она открыта</param>
    /// <returns>Карточка интерактивного пользователя</returns>
    private Task<IdentityUser?> FindAsync(Guid userId, IDbTransaction? transaction)
    {
        var selectUserQuery = BuildUserQuery() + $""" AND u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = {EntityMapper.ParameterName(SqlParameterNames.UserId)}""";
        return connection.QuerySingleOrDefaultAsync<IdentityUser>(selectUserQuery,
            new { userId, systemUserId = IdentityAccounts.SystemUserId }, transaction);
    }

    /// <summary>
    /// Проверяет существование и актуальность карточки
    /// </summary>
    /// <param name="userId">Изменяемая запись</param>
    /// <param name="version">Ожидаемая версия</param>
    /// <param name="transaction">Транзакция команды</param>
    /// <returns>Подтверждённая карточка</returns>
    private async Task<IdentityUser> RequireUserAsync(Guid userId, Guid version, IDbTransaction transaction)
    {
        var lockUserQuery = $"""SELECT {EntityMapper.ColName<UserProfileDal>(x => x.UserId)} FROM {EntityMapper.TbName<UserProfileDal>()} WHERE {EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = {EntityMapper.ParameterName(SqlParameterNames.UserId)} FOR UPDATE""";
        await connection.ExecuteAsync(lockUserQuery,
            new { userId }, transaction);
        var user = await FindAsync(userId, transaction) ?? throw new IdentityCommandException(404, IdentityErrorCodes.UserNotFound);
        return user.Version == version ? user : throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
    }

    /// <summary>
    /// Читает системную роль в транзакции изменения
    /// </summary>
    /// <param name="roleId">Идентификатор роли</param>
    /// <param name="transaction">Транзакция команды</param>
    /// <returns>Текущая конфигурация роли</returns>
    private async Task<IdentityRole> FindRoleAsync(Guid roleId, IDbTransaction transaction)
    {
        var selectRoleQuery = BuildRoleQuery() + $""" AND r.{EntityMapper.ColName<RoleDal>(x => x.Id)} = {EntityMapper.ParameterName(SqlParameterNames.RoleId)}""";
        return await connection.QuerySingleOrDefaultAsync<IdentityRole>(selectRoleQuery,
            new { roleId }, transaction) ?? throw new IdentityCommandException(404, IdentityErrorCodes.RoleNotFound);
    }

    /// <summary>
    /// Проверяет допустимость назначения каждой роли
    /// </summary>
    /// <param name="requested">Роли из команды</param>
    /// <param name="transaction">Транзакция команды</param>
    /// <returns>Уникальные действующие роли</returns>
    private async Task<Guid[]> RequireRolesAsync(Guid[] requested, IDbTransaction transaction)
    {
        var roleIds = requested.Distinct().ToArray();
        var selectRoleCommandQuery = $"""
            SELECT count(*) FROM {EntityMapper.TbName<RoleDal>()} WHERE {EntityMapper.ColName<RoleDal>(x => x.Code)} IS NOT NULL AND {EntityMapper.ColName<RoleDal>(x => x.IsActive)} AND {EntityMapper.ColName<RoleDal>(x => x.Id)} = ANY({EntityMapper.ParameterName(SqlParameterNames.RoleIds)})
            """;
        var valid = await connection.ExecuteScalarAsync<long>(selectRoleCommandQuery, new { roleIds }, transaction);
        if (roleIds.Length == 0 || valid != roleIds.Length)
        {
            throw new IdentityCommandException(400, IdentityErrorCodes.ActiveRoleRequired);
        }

        return roleIds;
    }

    /// <summary>
    /// Сохраняет полный набор назначений с реактивацией существующих связей
    /// </summary>
    /// <param name="userId">Получатель ролей</param>
    /// <param name="roleIds">Желаемый набор назначений</param>
    /// <param name="actor">Автор изменений</param>
    /// <param name="transaction">Транзакция команды</param>
    private async Task SetRolesAsync(Guid userId, Guid[] roleIds, Guid actor, IDbTransaction transaction)
    {
        var updateUserRoleQuery = $"""
            UPDATE {EntityMapper.TbName<UserRoleDal>()} SET {EntityMapper.ColName<UserRoleDal>(x => x.IsActive)} = false, {EntityMapper.ColName<UserRoleDal>(x => x.UpdatedBy)} = {EntityMapper.ParameterName(SqlParameterNames.Actor)}, {EntityMapper.ColName<UserRoleDal>(x => x.UpdatedAt)} = now()
                WHERE {EntityMapper.ColName<UserRoleDal>(x => x.UserId)} = {EntityMapper.ParameterName(SqlParameterNames.UserId)} AND {EntityMapper.ColName<UserRoleDal>(x => x.IsActive)} AND {EntityMapper.ColName<UserRoleDal>(x => x.RoleId)} <> ALL({EntityMapper.ParameterName(SqlParameterNames.RoleIds)});
            """;
        var parameters = new { userId, roleIds, actor };
        await connection.ExecuteAsync(updateUserRoleQuery, parameters, transaction);

        var insertUserRoleQuery = $"""
            INSERT INTO {EntityMapper.TbName<UserRoleDal>()} ({EntityMapper.ColName<UserRoleDal>(x => x.UserId)}, {EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}, {EntityMapper.ColName<UserRoleDal>(x => x.IsActive)}, {EntityMapper.ColName<UserRoleDal>(x => x.UpdatedBy)})
                SELECT {EntityMapper.ParameterName(SqlParameterNames.UserId)}, unnest({EntityMapper.ParameterName(SqlParameterNames.RoleIds)}), true, {EntityMapper.ParameterName(SqlParameterNames.Actor)}
                ON CONFLICT ({EntityMapper.ColName<UserRoleDal>(x => x.UserId)}, {EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}) DO UPDATE SET {EntityMapper.ColName<UserRoleDal>(x => x.IsActive)} = true, {EntityMapper.ColName<UserRoleDal>(x => x.UpdatedBy)} = {EntityMapper.ParameterName(SqlParameterNames.Actor)},
                    {EntityMapper.ColName<UserRoleDal>(x => x.UpdatedAt)} = now() WHERE NOT {EntityMapper.TbName<UserRoleDal>()}.{EntityMapper.ColName<UserRoleDal>(x => x.IsActive)};
            """;
        await connection.ExecuteAsync(insertUserRoleQuery, parameters, transaction);
    }

    /// <summary>
    /// Создаёт учётную запись и первую версию выдачи
    /// </summary>
    /// <param name="email">Адрес входа и доставки</param>
    /// <param name="fullName">ФИО пользователя</param>
    /// <param name="roleIds">Назначенные роли</param>
    /// <param name="actor">Инициатор создания</param>
    /// <param name="passwordHash">Хеш временного пароля</param>
    /// <param name="first">Признак первого администратора</param>
    /// <param name="transaction">Транзакция команды</param>
    /// <param name="initialOrganization">Необязательные оргданные, сохраняемые в той же транзакции</param>
    /// <returns>Метаданные выдачи доступа</returns>
    private async Task<IdentityInvitation> InsertAsync(string email, string fullName, Guid[] roleIds, Guid actor,
        string passwordHash, bool first, IDbTransaction transaction, InitialOrganizationRequest? initialOrganization = null)
    {
        var userId = Guid.NewGuid();
        var insertUserProfileQuery = $"""
            INSERT INTO {EntityMapper.TbName<UserProfileDal>()} ({EntityMapper.ColName<UserProfileDal>(x => x.Id)}, {EntityMapper.ColName<UserProfileDal>(x => x.UserId)}, {EntityMapper.ColName<UserProfileDal>(x => x.Email)}, {EntityMapper.ColName<UserProfileDal>(x => x.NormalizedEmail)}, {EntityMapper.ColName<UserProfileDal>(x => x.FullName)},
                {EntityMapper.ColName<UserProfileDal>(x => x.CreatedAt)}, {EntityMapper.ColName<UserProfileDal>(x => x.UpdatedAt)}, {EntityMapper.ColName<UserProfileDal>(x => x.UpdatedBy)}, {EntityMapper.ColName<UserProfileDal>(x => x.IsFirstAdministrator)})
            VALUES ({EntityMapper.ParameterName(SqlParameterNames.UserId)}, {EntityMapper.ParameterName(SqlParameterNames.UserId)}, {EntityMapper.ParameterName(SqlParameterNames.Email)}, {EntityMapper.ParameterName(SqlParameterNames.NormalizedEmail)}, {EntityMapper.ParameterName(SqlParameterNames.FullName)}, now(), now(), {EntityMapper.ParameterName(SqlParameterNames.Actor)}, {EntityMapper.ParameterName(SqlParameterNames.First)})
            """;
        await connection.ExecuteAsync(insertUserProfileQuery, new { userId, email = email.Trim(), normalizedEmail = email.Trim().ToLowerInvariant(), fullName, actor, first }, transaction);
        await SetRolesAsync(userId, roleIds, actor, transaction);
        if (initialOrganization is not null)
        {
            await organization.CreateForUserAsync(userId, initialOrganization, actor, transaction);
        }

        var user = await FindAsync(userId, transaction) ?? throw new IdentityCommandException(404, IdentityErrorCodes.UserNotFound);
        return await StartInvitationAsync(user, actor, passwordHash, false, transaction);
    }

    /// <summary>
    /// Фиксирует новый хеш и версию доставки, прекращая старые сеансы
    /// </summary>
    /// <param name="user">Получатель доступа</param>
    /// <param name="actor">Инициатор выдачи</param>
    /// <param name="passwordHash">Хеш временного пароля</param>
    /// <param name="activate">Признак активации в этой операции</param>
    /// <param name="transaction">Транзакция команды</param>
    /// <returns>Версия и данные письма</returns>
    private async Task<IdentityInvitation> StartInvitationAsync(IdentityUser user, Guid actor, string passwordHash,
        bool activate, IDbTransaction transaction)
    {
        if (string.IsNullOrWhiteSpace(user.Email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(user.Email))
        {
            throw new IdentityCommandException(409, IdentityErrorCodes.EmailRequired);
        }

        var version = Guid.NewGuid();
        var userId = user.Id;
        var updateUserProfileQuery = $"""
            UPDATE {EntityMapper.TbName<UserProfileDal>()} SET {EntityMapper.ColName<UserProfileDal>(x => x.IsActive)} = {EntityMapper.ColName<UserProfileDal>(x => x.IsActive)} OR {EntityMapper.ParameterName(SqlParameterNames.Activate)}, {EntityMapper.ColName<UserProfileDal>(x => x.MustChangePassword)} = true,
                            {EntityMapper.ColName<UserProfileDal>(x => x.TemporaryPasswordExpiresAt)} = now() + make_interval(days => {EntityMapper.ParameterName(SqlParameterNames.Days)}), {EntityMapper.ColName<UserProfileDal>(x => x.InvitationStatus)} = '{IdentityAccounts.InvitationPending}',
                            {EntityMapper.ColName<UserProfileDal>(x => x.InvitationVersion)} = {EntityMapper.ParameterName(SqlParameterNames.Version)}, {EntityMapper.ColName<UserProfileDal>(x => x.InvitationStartedAt)} = now(), {EntityMapper.ColName<UserProfileDal>(x => x.UpdatedAt)} = now(),
                            {EntityMapper.ColName<UserProfileDal>(x => x.UpdatedBy)} = {EntityMapper.ParameterName(SqlParameterNames.Actor)}, {EntityMapper.ColName<UserProfileDal>(x => x.Version)} = gen_random_uuid(), {EntityMapper.ColName<UserProfileDal>(x => x.AccessVersion)} = {EntityMapper.ColName<UserProfileDal>(x => x.AccessVersion)} + 1
                            WHERE {EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = {EntityMapper.ParameterName(SqlParameterNames.UserId)}
            """;
        await connection.ExecuteAsync(updateUserProfileQuery,
            new { userId, version, actor, activate, days = IdentityAccounts.TemporaryPasswordDays }, transaction);
        var insertUserCredentialQuery2 = $"""
            INSERT INTO {EntityMapper.TbName<UserCredentialDal>()} ({EntityMapper.ColName<UserCredentialDal>(x => x.Id)}, {EntityMapper.ColName<UserCredentialDal>(x => x.UserId)}, {EntityMapper.ColName<UserCredentialDal>(x => x.Password)}, {EntityMapper.ColName<UserCredentialDal>(x => x.PasswordChangedAt)})
                            VALUES (gen_random_uuid(), {EntityMapper.ParameterName(SqlParameterNames.UserId)}, {EntityMapper.ParameterName(SqlParameterNames.PasswordHash)}, now())
                            ON CONFLICT ({EntityMapper.ColName<UserCredentialDal>(x => x.UserId)}) DO UPDATE SET {EntityMapper.ColName<UserCredentialDal>(x => x.Password)} = {EntityMapper.ParameterName(SqlParameterNames.PasswordHash)}, {EntityMapper.ColName<UserCredentialDal>(x => x.PasswordChangedAt)} = now()
            """;
        await connection.ExecuteAsync(insertUserCredentialQuery2, new { userId, passwordHash }, transaction);
        await RevokeSessionsAsync(userId, SessionRevokeReasons.PasswordChanged, transaction);
        var selectAssignedRoleNamesQuery = $"""
            SELECT r.{EntityMapper.ColName<RoleDal>(x => x.Name)} FROM {EntityMapper.TbName<UserRoleDal>()} a JOIN {EntityMapper.TbName<RoleDal>()} r ON r.{EntityMapper.ColName<RoleDal>(x => x.Id)} = a.{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}
            WHERE a.{EntityMapper.ColName<UserRoleDal>(x => x.UserId)} = {EntityMapper.ParameterName(SqlParameterNames.UserId)} AND a.{EntityMapper.ColName<UserRoleDal>(x => x.IsActive)} AND r.{EntityMapper.ColName<RoleDal>(x => x.IsActive)} AND r.{EntityMapper.ColName<RoleDal>(x => x.Code)} IS NOT NULL ORDER BY r.{EntityMapper.ColName<RoleDal>(x => x.Name)}
            """;
        var roles = await connection.QueryAsync<string>(selectAssignedRoleNamesQuery, new { userId }, transaction);
        return new IdentityInvitation { UserId = userId, Version = version, Email = user.Email,
            FullName = user.FullName, Roles = roles.ToArray(),
            Responsibilities = await organization.ReadResponsibilitiesAsync(userId, transaction) };
    }

    /// <inheritdoc/>
    public Task<IdentityUser> DeactivateAsync(Guid actor, Guid sessionId, Guid userId, Guid version)
    {
        return AdministrationTransaction.WriteAsync(connection, actor, sessionId, async transaction =>
        {
            if (actor == userId)
            {
                throw new IdentityCommandException(409, OrganizationErrors.SelfDeactivation);
            }

            var user = await RequireUserAsync(userId, version, transaction);
            if (!user.IsActive)
            {
                return user;
            }

            await organization.DeactivateForUserAsync(userId, actor, transaction);
            var query = $"""
                UPDATE {EntityMapper.TbName<UserProfileDal>()}
                SET {EntityMapper.ColName<UserProfileDal>(x => x.IsActive)} = false,
                    {EntityMapper.ColName<UserProfileDal>(x => x.UpdatedBy)} = {EntityMapper.ParameterName(nameof(actor))},
                    {EntityMapper.ColName<UserProfileDal>(x => x.UpdatedAt)} = now(),
                    {EntityMapper.ColName<UserProfileDal>(x => x.AccessVersion)} = {EntityMapper.ColName<UserProfileDal>(x => x.AccessVersion)} + 1,
                    {EntityMapper.ColName<UserProfileDal>(x => x.Version)} = gen_random_uuid(),
                    {EntityMapper.ColName<UserProfileDal>(x => x.InvitationVersion)} = NULL,
                    {EntityMapper.ColName<UserProfileDal>(x => x.InvitationStatus)} = CASE
                        WHEN {EntityMapper.ColName<UserProfileDal>(x => x.InvitationStatus)} = '{IdentityAccounts.InvitationPending}'
                        THEN '{IdentityAccounts.InvitationFailed}' ELSE {EntityMapper.ColName<UserProfileDal>(x => x.InvitationStatus)}
                    END
                WHERE {EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = {EntityMapper.ParameterName(nameof(userId))}
                    AND {EntityMapper.ColName<UserProfileDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(version))}
                """;
            if (await connection.ExecuteAsync(query, new { actor, userId, version }, transaction) != 1)
            {
                throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
            }

            await EnsureAdministratorAsync(transaction);
            await RevokeSessionsAsync(userId, SessionRevokeReasons.UserDeactivated, transaction);
            return await FindAsync(userId, transaction) ?? throw new IdentityCommandException(404, IdentityErrorCodes.UserNotFound);
        });
    }

    /// <summary>Отзывает access-сессии и refresh-токены одной транзакцией</summary>
    /// <param name="userId">Владелец сессий</param>
    /// <param name="reason">Причина отзыва</param>
    /// <param name="transaction">Транзакция изменения пользователя</param>
    private async Task RevokeSessionsAsync(Guid userId, string reason, IDbTransaction transaction)
    {
        var updateSessionQuery3 = $"""
            UPDATE {EntityMapper.TbName<SessionDal>()} SET {EntityMapper.ColName<SessionDal>(x => x.RevokedAt)} = now(), {EntityMapper.ColName<SessionDal>(x => x.UpdatedAt)} = now(), {EntityMapper.ColName<SessionDal>(x => x.RevokeReason)} = {EntityMapper.ParameterName(SqlParameterNames.Reason)},
                            {EntityMapper.ColName<SessionDal>(x => x.Version)} = gen_random_uuid() WHERE {EntityMapper.ColName<SessionDal>(x => x.UserId)} = {EntityMapper.ParameterName(SqlParameterNames.UserId)} AND {EntityMapper.ColName<SessionDal>(x => x.RevokedAt)} IS NULL
            """;
        await connection.ExecuteAsync(updateSessionQuery3, new { userId, reason }, transaction);
        var updateRefreshTokenQuery4 = $"""
            UPDATE {EntityMapper.TbName<RefreshTokenDal>()} t SET {EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)} = now() WHERE t.{EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt)} IS NULL AND EXISTS (
                            SELECT FROM {EntityMapper.TbName<SessionDal>()} s WHERE s.{EntityMapper.ColName<SessionDal>(x => x.Id)} = t.{EntityMapper.ColName<RefreshTokenDal>(x => x.SessionId)} AND s.{EntityMapper.ColName<SessionDal>(x => x.UserId)} = {EntityMapper.ParameterName(SqlParameterNames.UserId)})
            """;
        await connection.ExecuteAsync(updateRefreshTokenQuery4, new { userId }, transaction);
    }
}
