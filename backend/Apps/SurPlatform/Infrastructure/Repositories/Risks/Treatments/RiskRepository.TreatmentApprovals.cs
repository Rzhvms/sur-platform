using System.Data;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.UseCases.Treatments.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Repositories.Risks.Helpers;

namespace Infrastructure.Repositories.Risks;

/// <summary>Отправка и принятие решений по согласованию мер</summary>
internal sealed partial class RiskRepository
{
    /// <summary>Псевдоним процесса в запросах доступа к согласованиям</summary>
    private const string ApprovalProcessAlias = "process";

    /// <inheritdoc/>
    public Task<TreatmentApprovalDal> SubmitTreatmentAsync(RiskActor actor, Guid id, TreatmentActionRequest request)
    {
        return WriteAsync(actor, TreatmentRules.Execute, async transaction =>
        {
            await RequireRightAsync(actor, RiskRules.View, transaction);
            var measure = await ReadAsync<TreatmentDal>(id, EntityMapper.ColName<TreatmentDal>(x => x.Id), transaction);
            RequireVersion(measure.Version, request.Version);
            RequireTreatmentState(measure.Status == TreatmentRules.Draft);
            await RequireTreatmentOwnerAsync(actor, measure, transaction);
            await RequireTreatmentCycleAsync(measure.RiskId, RiskRules.Active, transaction);
            await SelectTreatmentLinkAsync(measure.RiskId, measure.RiskProcessId, transaction);
            var settings = await ReadTreatmentSettingsAsync(transaction);
            var level = ApprovalLevel(measure.CostPlanned, settings);
            if (level is null)
            {
                throw new IdentityCommandException(409, TreatmentRules.Conflict);
            }
            var processId = measure.ProcessId;
            var scope = ApproverExistsSql(ApprovalProcessAlias, EntityMapper.ParameterName(nameof(level)), false);
            var query = $"""
                SELECT EXISTS (SELECT FROM {EntityMapper.TbName<ProcessDal>()} process WHERE process.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(processId))}
                    AND {scope})
                """;
            var rights = ApprovalRights();
            if (!await connection.ExecuteScalarAsync<bool>(query, new { processId, level, rights.owner, rights.director, rights.ceo,
                rights.ownerRight, rights.directorRight, rights.ceoRight, rights.specialist }, transaction))
            {
                throw new IdentityCommandException(409, TreatmentRules.NoApprover);
            }
            var row = new TreatmentApprovalDal
            {
                Id = Guid.NewGuid(), TreatmentId = id, Status = TreatmentRules.Pending, RequiredLevel = level,
                CostPlanned = measure.CostPlanned, TriggerThreshold = level switch
                {
                    OrganizationRoles.Owner => settings.OwnerThreshold,
                    OrganizationRoles.Director => settings.DirectorThreshold,
                    _ => settings.CeoThreshold
                },
                OwnerThreshold = settings.OwnerThreshold, DirectorThreshold = settings.DirectorThreshold,
                CeoThreshold = settings.CeoThreshold, RequestedById = actor.UserId
            };
            await SaveTreatmentStatusAsync(measure, TreatmentRules.Pending, actor.UserId, Guid.NewGuid(), transaction);
            var insertQuery = $"""
                INSERT INTO {EntityMapper.TbName<TreatmentApprovalDal>()}
                    ({EntityMapper.ColName<TreatmentApprovalDal>(x => x.Id)}, {EntityMapper.ColName<TreatmentApprovalDal>(x => x.TreatmentId)}, {EntityMapper.ColName<TreatmentApprovalDal>(x => x.Status)}, {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RequiredLevel)},
                     {EntityMapper.ColName<TreatmentApprovalDal>(x => x.CostPlanned)}, {EntityMapper.ColName<TreatmentApprovalDal>(x => x.TriggerThreshold)}, {EntityMapper.ColName<TreatmentApprovalDal>(x => x.OwnerThreshold)}, {EntityMapper.ColName<TreatmentApprovalDal>(x => x.DirectorThreshold)}, {EntityMapper.ColName<TreatmentApprovalDal>(x => x.CeoThreshold)},
                     {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RequestedById)})
                VALUES ({EntityMapper.ParameterName(nameof(TreatmentApprovalDal.Id))}, {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.TreatmentId))}, {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.Status))}, {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.RequiredLevel))},
                        {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.CostPlanned))}, {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.TriggerThreshold))}, {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.OwnerThreshold))}, {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.DirectorThreshold))}, {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.CeoThreshold))},
                        {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.RequestedById))}) RETURNING *
                """;
            return await connection.QuerySingleAsync<TreatmentApprovalDal>(insertQuery, row, transaction);
        });
    }

    /// <inheritdoc/>
    public Task<TreatmentApprovalDal> DecideTreatmentApprovalAsync(RiskActor actor, Guid id, string decision, TreatmentActionRequest request)
    {
        return WriteAsync(actor, null, async transaction =>
        {
            var revoke = decision == TreatmentRules.Revoked;
            var admin = await HasRightAsync(actor, IdentityRights.AdminCode, transaction);
            if (!admin && (revoke || !await HasAnyApprovalRightAsync(actor, transaction)))
            {
                throw new IdentityCommandException(403, IdentityErrorCodes.AccessDenied);
            }
            var approval = await ReadAsync<TreatmentApprovalDal>(id, EntityMapper.ColName<TreatmentApprovalDal>(x => x.Id), transaction);
            var measure = await ReadAsync<TreatmentDal>(approval.TreatmentId, EntityMapper.ColName<TreatmentDal>(x => x.Id), transaction);
            if (!admin)
            {
                var processId = measure.ProcessId;
                var level = approval.RequiredLevel;
                var userId = actor.UserId;
                var rights = ApprovalRights();
                var scope = ApproverExistsSql(ApprovalProcessAlias, EntityMapper.ParameterName(nameof(level)), true);
                var query = $"""
                    SELECT EXISTS (SELECT FROM {EntityMapper.TbName<ProcessDal>()} process WHERE process.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(processId))} AND {scope})
                    """;
                if (!await connection.ExecuteScalarAsync<bool>(query, new { processId, level, userId, rights.owner,
                    rights.director, rights.ceo, rights.ownerRight, rights.directorRight, rights.ceoRight, rights.specialist }, transaction))
                {
                    throw new IdentityCommandException(403, IdentityErrorCodes.AccessDenied);
                }
            }

            RequireVersion(approval.Version, request.Version);
            var pending = approval.Status == TreatmentRules.Pending && measure.Status == TreatmentRules.Pending;
            var approved = approval.Status == TreatmentRules.Approved && measure.Status == TreatmentRules.Approved;
            RequireTreatmentState((decision is TreatmentRules.Approved or TreatmentRules.Rejected or TreatmentRules.Revoked) &&
                (pending || (revoke && approved)));
            var reason = decision == TreatmentRules.Approved ? request.Reason?.Trim() : RequiredText(request.Reason);
            var target = decision == TreatmentRules.Approved ? TreatmentRules.Approved :
                approved ? TreatmentRules.Cancelled : TreatmentRules.Draft;
            var updated = await UpdateApprovalAsync(approval, actor.UserId, decision, reason, revoke, transaction);
            var operationId = Guid.NewGuid();
            await SaveTreatmentStatusAsync(measure, target, actor.UserId, operationId, transaction);
            if (target == TreatmentRules.Cancelled)
            {
                await StartReviewAfterTreatmentAsync(measure, request.Confirmed, actor.UserId, operationId, transaction);
            }
            return updated;
        });
    }

    /// <summary>Записывает решение или отзыв с сохранением исходного решения</summary>
    /// <param name="approval">Текущая версия запроса</param>
    /// <param name="actor">Инициатор действия</param>
    /// <param name="decision">Целевой статус</param>
    /// <param name="reason">Причина или комментарий</param>
    /// <param name="revoke">Отзыв запроса</param>
    /// <param name="transaction">Транзакция запроса и меры</param>
    /// <returns>Обновлённый запрос</returns>
    private async Task<TreatmentApprovalDal> UpdateApprovalAsync(TreatmentApprovalDal approval, Guid actor, string decision,
        string? reason, bool revoke, IDbTransaction transaction)
    {
        var decisionFields = $"""
            {EntityMapper.ColName<TreatmentApprovalDal>(x => x.DecidedById)} = {EntityMapper.ParameterName(nameof(actor))}, {EntityMapper.ColName<TreatmentApprovalDal>(x => x.DecidedAt)} = clock_timestamp(),
            {EntityMapper.ColName<TreatmentApprovalDal>(x => x.DecisionReason)} = {EntityMapper.ParameterName(nameof(reason))}
            """;
        var revokeFields = $"""
            {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RevokedById)} = {EntityMapper.ParameterName(nameof(actor))}, {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RevokedAt)} = clock_timestamp(),
            {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RevokeReason)} = {EntityMapper.ParameterName(nameof(reason))}
            """;
        var query = $"""
            UPDATE {EntityMapper.TbName<TreatmentApprovalDal>()} SET {EntityMapper.ColName<TreatmentApprovalDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(decision))},
                {EntityMapper.ColName<TreatmentApprovalDal>(x => x.Version)} = gen_random_uuid(), {(revoke ? revokeFields : decisionFields)}
            WHERE {EntityMapper.ColName<TreatmentApprovalDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(approval.Id))} AND {EntityMapper.ColName<TreatmentApprovalDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(approval.Version))} RETURNING *
            """;
        return await connection.QuerySingleOrDefaultAsync<TreatmentApprovalDal>(query,
            new { approval.Id, approval.Version, actor, decision, reason }, transaction)
            ?? throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
    }

    /// <inheritdoc/>
    public async Task<TreatmentApprovalQueue> ListTreatmentApprovalsAsync(RiskActor actor, int offset, int limit)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        var admin = await HasRightAsync(actor, IdentityRights.AdminCode, transaction);
        if (!admin && !await HasAnyApprovalRightAsync(actor, transaction))
        {
            throw new IdentityCommandException(403, IdentityErrorCodes.AccessDenied);
        }
        var pending = TreatmentRules.Pending;
        var userId = actor.UserId;
        var rights = ApprovalRights();
        var scope = ApproverExistsSql(ApprovalProcessAlias, $"a.{EntityMapper.ColName<TreatmentApprovalDal>(x => x.RequiredLevel)}", true);
        var source = $"""
            FROM {EntityMapper.TbName<TreatmentApprovalDal>()} a JOIN {EntityMapper.TbName<TreatmentDal>()} m ON m.{EntityMapper.ColName<TreatmentDal>(x => x.Id)} = a.{EntityMapper.ColName<TreatmentApprovalDal>(x => x.TreatmentId)}
            JOIN {EntityMapper.TbName<ProcessDal>()} process ON process.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = m.{EntityMapper.ColName<TreatmentDal>(x => x.ProcessId)}
            WHERE a.{EntityMapper.ColName<TreatmentApprovalDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(pending))} AND m.{EntityMapper.ColName<TreatmentDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(pending))} AND ({EntityMapper.ParameterName(nameof(admin))} OR {scope})
            """;
        var countQuery = $"SELECT count(*) {source}";
        var pageQuery = $"""
            SELECT a.* {source}
            ORDER BY a.{EntityMapper.ColName<TreatmentApprovalDal>(x => x.RequestedAt)}, a.{EntityMapper.ColName<TreatmentApprovalDal>(x => x.Id)} OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var parameters = new { admin, userId, pending, offset, limit, rights.owner, rights.director, rights.ceo,
            rights.ownerRight, rights.directorRight, rights.ceoRight, rights.specialist };
        var count = await connection.ExecuteScalarAsync<long>(countQuery, parameters, transaction);
        var rows = (await connection.QueryAsync<TreatmentApprovalDal>(pageQuery, parameters, transaction)).ToArray();
        transaction.Commit();
        return new TreatmentApprovalQueue(rows, count);
    }

    /// <inheritdoc/>
    public async Task<TreatmentApprovalDal[]> ListTreatmentApprovalHistoryAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        await ReadAsync<TreatmentDal>(id, EntityMapper.ColName<TreatmentDal>(x => x.Id), transaction);
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<TreatmentApprovalDal>()}
            WHERE {EntityMapper.ColName<TreatmentApprovalDal>(x => x.TreatmentId)} = {EntityMapper.ParameterName(nameof(id))}
            ORDER BY {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RequestedAt)} DESC,
                {EntityMapper.ColName<TreatmentApprovalDal>(x => x.Id)} DESC
            OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var rows = await connection.QueryAsync<TreatmentApprovalDal>(query, new { id, offset, limit }, transaction);
        transaction.Commit();
        return rows.ToArray();
    }

    /// <summary>Проверяет актуальность хотя бы одного права согласования</summary>
    /// <param name="actor">Инициатор</param>
    /// <param name="transaction">Согласованный снимок</param>
    /// <returns>Доступен ли раздел согласований</returns>
    private async Task<bool> HasAnyApprovalRightAsync(RiskActor actor, IDbTransaction transaction)
    {
        return await HasRightAsync(actor, TreatmentRules.ApproveOwner, transaction)
            || await HasRightAsync(actor, TreatmentRules.ApproveDirector, transaction)
            || await HasRightAsync(actor, TreatmentRules.ApproveCeo, transaction);
    }

    /// <summary>Подготавливает параметры уровней, назначений и прав для проверки согласующих</summary>
    /// <returns>Коды бизнес-прав и организационных уровней</returns>
    private static (string owner, string director, string ceo, string specialist, string ownerRight, string directorRight, string ceoRight) ApprovalRights()
    {
        return (OrganizationRoles.Owner, OrganizationRoles.Director, OrganizationRoles.Ceo, OrganizationRoles.RiskSpecialist,
            TreatmentRules.ApproveOwner, TreatmentRules.ApproveDirector, TreatmentRules.ApproveCeo);
    }

    /// <summary>Формирует проверку кандидата с явно назначенным правом уровня</summary>
    /// <param name="process">Доверенный псевдоним процесса</param>
    /// <param name="level">Доверенное выражение уровня из колонки или параметра</param>
    /// <param name="onlyActor">Ограничивает кандидата текущим пользователем</param>
    /// <returns>Предикат доступности согласующего</returns>
    private static string ApproverExistsSql(string process, string level, bool onlyActor)
    {
        const string userId = "userId";
        const string owner = "owner";
        const string director = "director";
        const string ceo = "ceo";
        const string specialist = "specialist";
        const string ownerRight = "ownerRight";
        const string directorRight = "directorRight";
        const string ceoRight = "ceoRight";
        var scope = onlyActor ? $"AND p.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)} = {EntityMapper.ParameterName(userId)}" : string.Empty;
        var query = $"""
            EXISTS (
                SELECT FROM {EntityMapper.TbName<OrgPersonDal>()} p JOIN {EntityMapper.TbName<UserProfileDal>()} u ON u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = p.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)}
                WHERE p.{EntityMapper.ColName<OrgPersonDal>(x => x.IsActive)} AND u.{EntityMapper.ColName<UserProfileDal>(x => x.IsActive)} {scope}
                  AND EXISTS (SELECT FROM {EntityMapper.TbName<UserRoleDal>()} ur JOIN {EntityMapper.TbName<RoleDal>()} r ON r.{EntityMapper.ColName<RoleDal>(x => x.Id)} = ur.{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}
                    JOIN {EntityMapper.TbName<RoleRightDal>()} rr ON rr.{EntityMapper.ColName<RoleRightDal>(x => x.RoleId)} = r.{EntityMapper.ColName<RoleDal>(x => x.Id)}
                    JOIN {EntityMapper.TbName<RightDal>()} rt ON rt.{EntityMapper.ColName<RightDal>(x => x.Id)} = rr.{EntityMapper.ColName<RoleRightDal>(x => x.RightId)}
                    WHERE ur.{EntityMapper.ColName<UserRoleDal>(x => x.UserId)} = p.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)} AND ur.{EntityMapper.ColName<UserRoleDal>(x => x.IsActive)} AND r.{EntityMapper.ColName<RoleDal>(x => x.IsActive)}
                      AND r.{EntityMapper.ColName<RoleDal>(x => x.Code)} IS NOT NULL AND rt.{EntityMapper.ColName<RightDal>(x => x.Code)} = CASE {level}
                          WHEN {EntityMapper.ParameterName(nameof(owner))} THEN {EntityMapper.ParameterName(nameof(ownerRight))} WHEN {EntityMapper.ParameterName(nameof(director))} THEN {EntityMapper.ParameterName(nameof(directorRight))} ELSE {EntityMapper.ParameterName(nameof(ceoRight))} END)
                  AND (({level} = {EntityMapper.ParameterName(nameof(owner))} AND p.{EntityMapper.ColName<OrgPersonDal>(x => x.Id)} = {process}.{EntityMapper.ColName<ProcessDal>(x => x.OwnerId)})
                    OR EXISTS (SELECT FROM {EntityMapper.TbName<ResponsibilityDal>()} responsibility
                      JOIN {EntityMapper.TbName<UserRoleDal>()} ur ON ur.{EntityMapper.ColName<UserRoleDal>(x => x.UserId)} = p.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)} AND ur.{EntityMapper.ColName<UserRoleDal>(x => x.IsActive)}
                      JOIN {EntityMapper.TbName<RoleDal>()} r ON r.{EntityMapper.ColName<RoleDal>(x => x.Id)} = ur.{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)} AND r.{EntityMapper.ColName<RoleDal>(x => x.IsActive)}
                          AND r.{EntityMapper.ColName<RoleDal>(x => x.Code)} = responsibility.{EntityMapper.ColName<ResponsibilityDal>(x => x.ResponsibilityType)}
                      WHERE responsibility.{EntityMapper.ColName<ResponsibilityDal>(x => x.PersonId)} = p.{EntityMapper.ColName<OrgPersonDal>(x => x.Id)} AND responsibility.{EntityMapper.ColName<ResponsibilityDal>(x => x.IsActive)}
                        AND (({level} = {EntityMapper.ParameterName(nameof(ceo))} AND responsibility.{EntityMapper.ColName<ResponsibilityDal>(x => x.ResponsibilityType)} = {EntityMapper.ParameterName(nameof(ceo))} AND responsibility.{EntityMapper.ColName<ResponsibilityDal>(x => x.DirectorateId)} IS NULL)
                          OR (responsibility.{EntityMapper.ColName<ResponsibilityDal>(x => x.DirectorateId)} = {process}.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)}
                            AND (({level} = {EntityMapper.ParameterName(nameof(director))} AND responsibility.{EntityMapper.ColName<ResponsibilityDal>(x => x.ResponsibilityType)} = {EntityMapper.ParameterName(nameof(director))})
                              OR ({level} = {EntityMapper.ParameterName(nameof(ceo))} AND responsibility.{EntityMapper.ColName<ResponsibilityDal>(x => x.ResponsibilityType)} = {EntityMapper.ParameterName(nameof(specialist))})))))))
            """;
        return query;
    }
}
