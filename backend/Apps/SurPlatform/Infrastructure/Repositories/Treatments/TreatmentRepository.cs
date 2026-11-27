using Infrastructure.Repositories.History.Helpers;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.Ports.Repositories;
using Application.UseCases.Treatments.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Migrations;
using Infrastructure.Observability;
using Infrastructure.Repositories.Assessments.Interfaces;
using Infrastructure.Repositories.Jira.Interfaces;
using Infrastructure.Repositories.Risks.Interfaces;
using Infrastructure.Repositories.Sessions.Interfaces;
using Infrastructure.Repositories.Shared;
using Infrastructure.Repositories.Treatments.Interfaces;
using Npgsql;
using System.Data;
using Domain.DalModels.Organization;
using Domain.DalModels.Rights;
using Domain.DalModels.Risks;
using Domain.DalModels.Roles;
using Domain.DalModels.Treatments;
using Domain.DalModels.Users;

namespace Infrastructure.Repositories.Treatments;

/// <summary>Меры, исполнители и согласования</summary>
internal sealed class TreatmentRepository : ClientRepository, ITreatmentRepository
{
    /// <summary>Поручения Jira и режим управления мерой</summary>
    private readonly IJiraStateRepository _jiraStateRepository;

    /// <summary>Текущая оценка и версии состояния риска</summary>
    private readonly IRiskAssessmentStateRepository _riskAssessmentStateRepository;

    /// <summary>Паспорт, связи процессов и технический пересчет риска</summary>
    private readonly IRiskStateRepository _riskStateRepository;

    /// <summary>Пересмотр после завершения последней меры</summary>
    private readonly ITreatmentLifecycleRepository _treatmentLifecycleRepository;

    /// <summary>Назначения и переходы состояния мер</summary>
    private readonly ITreatmentStateRepository _treatmentStateRepository;

    /// <summary>Инициализирует операции выбранной клиентской базы</summary>
    /// <param name="connection">Подключение выбранной клиентской базы</param>
    /// <param name="sessions">Проверка сессии и эффективных прав в транзакции</param>
    /// <param name="operations">Метрики операций базы</param>
    /// <param name="jiraStateRepository">Поручения Jira и режим управления мерой</param>
    /// <param name="riskAssessmentStateRepository">Текущая оценка и версии состояния риска</param>
    /// <param name="riskStateRepository">Паспорт, связи процессов и технический пересчет риска</param>
    /// <param name="treatmentLifecycleRepository">Пересмотр после завершения последней меры</param>
    /// <param name="treatmentStateRepository">Назначения и переходы состояния мер</param>
    public TreatmentRepository(
        IDbConnection connection,
        ITransactionalSessionRepository sessions,
        OperationsMetrics operations,
        IJiraStateRepository jiraStateRepository,
        IRiskAssessmentStateRepository riskAssessmentStateRepository,
        IRiskStateRepository riskStateRepository,
        ITreatmentLifecycleRepository treatmentLifecycleRepository,
        ITreatmentStateRepository treatmentStateRepository)
        : base(connection, sessions, operations)
    {
        _jiraStateRepository = jiraStateRepository;
        _riskAssessmentStateRepository = riskAssessmentStateRepository;
        _riskStateRepository = riskStateRepository;
        _treatmentLifecycleRepository = treatmentLifecycleRepository;
        _treatmentStateRepository = treatmentStateRepository;
    }

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
            TreatmentStateRepository.RequireTreatmentState(measure.Status == TreatmentRules.Draft);
            await RequireTreatmentOwnerAsync(actor, measure, transaction);
            await _treatmentStateRepository.RequireTreatmentCycleAsync(measure.RiskId, RiskRules.Active, transaction);
            await _treatmentStateRepository.SelectTreatmentLinkAsync(measure.RiskId, measure.RiskProcessId, transaction);
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
            if (!await _connection.ExecuteScalarAsync<bool>(query, new { processId, level, rights.owner, rights.director, rights.ceo,
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
            await _treatmentStateRepository.SaveTreatmentStatusAsync(measure, TreatmentRules.Pending, actor.UserId, Guid.NewGuid(), transaction);
            var insertQuery = $"""
                INSERT INTO {EntityMapper.TbName<TreatmentApprovalDal>()}
                    ({EntityMapper.ColName<TreatmentApprovalDal>(x => x.Id)}, {EntityMapper.ColName<TreatmentApprovalDal>(x => x.TreatmentId)}, {EntityMapper.ColName<TreatmentApprovalDal>(x => x.Status)}, {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RequiredLevel)},
                     {EntityMapper.ColName<TreatmentApprovalDal>(x => x.CostPlanned)}, {EntityMapper.ColName<TreatmentApprovalDal>(x => x.TriggerThreshold)}, {EntityMapper.ColName<TreatmentApprovalDal>(x => x.OwnerThreshold)}, {EntityMapper.ColName<TreatmentApprovalDal>(x => x.DirectorThreshold)}, {EntityMapper.ColName<TreatmentApprovalDal>(x => x.CeoThreshold)},
                     {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RequestedById)})
                VALUES ({EntityMapper.ParameterName(nameof(TreatmentApprovalDal.Id))}, {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.TreatmentId))}, {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.Status))}, {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.RequiredLevel))},
                        {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.CostPlanned))}, {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.TriggerThreshold))}, {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.OwnerThreshold))}, {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.DirectorThreshold))}, {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.CeoThreshold))},
                        {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.RequestedById))}) RETURNING *
                """;
            return await _connection.QuerySingleAsync<TreatmentApprovalDal>(insertQuery, row, transaction);
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
                if (!await _connection.ExecuteScalarAsync<bool>(query, new { processId, level, userId, rights.owner,
                    rights.director, rights.ceo, rights.ownerRight, rights.directorRight, rights.ceoRight, rights.specialist }, transaction))
                {
                    throw new IdentityCommandException(403, IdentityErrorCodes.AccessDenied);
                }
            }

            RequireVersion(approval.Version, request.Version);
            var pending = approval.Status == TreatmentRules.Pending && measure.Status == TreatmentRules.Pending;
            var approved = approval.Status == TreatmentRules.Approved && measure.Status == TreatmentRules.Approved;
            TreatmentStateRepository.RequireTreatmentState((decision is TreatmentRules.Approved or TreatmentRules.Rejected or TreatmentRules.Revoked) &&
                (pending || (revoke && approved)));
            var reason = decision == TreatmentRules.Approved ? request.Reason?.Trim() : RequiredText(request.Reason);
            var target = decision == TreatmentRules.Approved ? TreatmentRules.Approved :
                approved ? TreatmentRules.Cancelled : TreatmentRules.Draft;
            var updated = await UpdateApprovalAsync(approval, actor.UserId, decision, reason, revoke, transaction);
            var operationId = Guid.NewGuid();
            await _treatmentStateRepository.SaveTreatmentStatusAsync(measure, target, actor.UserId, operationId, transaction);
            if (target == TreatmentRules.Cancelled)
            {
                await _treatmentLifecycleRepository.StartReviewAfterTreatmentAsync(measure, request.Confirmed, actor.UserId, operationId, transaction);
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
        return await _connection.QuerySingleOrDefaultAsync<TreatmentApprovalDal>(query,
            new { approval.Id, approval.Version, actor, decision, reason }, transaction)
            ?? throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
    }

    /// <inheritdoc/>
    public async Task<TreatmentApprovalQueue> ListTreatmentApprovalsAsync(RiskActor actor, int offset, int limit)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
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
            SELECT a.*, m.{EntityMapper.ColName<TreatmentDal>(x => x.Title)} AS {EntityMapper.ColName<TreatmentDal>(x => x.Title)} {source}
            ORDER BY a.{EntityMapper.ColName<TreatmentApprovalDal>(x => x.RequestedAt)}, a.{EntityMapper.ColName<TreatmentApprovalDal>(x => x.Id)} OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var parameters = new { admin, userId, pending, offset, limit, rights.owner, rights.director, rights.ceo,
            rights.ownerRight, rights.directorRight, rights.ceoRight, rights.specialist };
        var count = await _connection.ExecuteScalarAsync<long>(countQuery, parameters, transaction);
        var titles = new Dictionary<Guid, string>();
        var rows = (await _connection.QueryAsync<TreatmentApprovalDal, string, TreatmentApprovalDal>(pageQuery,
            (approval, title) =>
            {
                titles.Add(approval.TreatmentId, title);
                return approval;
            }, parameters, transaction, splitOn: EntityMapper.ColName<TreatmentDal>(x => x.Title, false))).ToArray();
        transaction.Commit();
        return new TreatmentApprovalQueue(rows, count) { TreatmentTitles = titles };
    }

    /// <inheritdoc/>
    public async Task<TreatmentApprovalDal[]> ListTreatmentApprovalHistoryAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        await ReadAsync<TreatmentDal>(id, EntityMapper.ColName<TreatmentDal>(x => x.Id), transaction);
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<TreatmentApprovalDal>()}
            WHERE {EntityMapper.ColName<TreatmentApprovalDal>(x => x.TreatmentId)} = {EntityMapper.ParameterName(nameof(id))}
            ORDER BY {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RequestedAt)} DESC,
                {EntityMapper.ColName<TreatmentApprovalDal>(x => x.Id)} DESC
            OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var rows = await _connection.QueryAsync<TreatmentApprovalDal>(query, new { id, offset, limit }, transaction);
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

    /// <inheritdoc/>
    public Task<TreatmentDal> AssignTreatmentAsync(RiskActor actor, Guid id, bool dispatch, TreatmentActionRequest request)
    {
        return WriteAsync(actor, TreatmentRules.Assign, async transaction =>
        {
            await RequireRightAsync(actor, RiskRules.View, transaction);
            var measure = await ReadAsync<TreatmentDal>(id, EntityMapper.ColName<TreatmentDal>(x => x.Id), transaction);
            RequireVersion(measure.Version, request.Version);
            await RequireTreatmentOwnerAsync(actor, measure, transaction);
            if (dispatch)
            {
                await RequireRightAsync(actor, TreatmentRules.Execute, transaction);
                TreatmentStateRepository.RequireTreatmentState(measure.Status is TreatmentRules.Draft or TreatmentRules.Approved);
                await _treatmentStateRepository.RequireTreatmentCycleAsync(measure.RiskId, RiskRules.Active, transaction);
                await _treatmentStateRepository.SelectTreatmentLinkAsync(measure.RiskId, measure.RiskProcessId, transaction);
                if (measure.Status == TreatmentRules.Draft)
                {
                    var settings = await ReadTreatmentSettingsAsync(transaction);
                    TreatmentStateRepository.RequireTreatmentState(ApprovalLevel(measure.CostPlanned, settings) is null);
                }
                else
                {
                    var approved = TreatmentRules.Approved;
                    var approvalQuery = $"""
                        SELECT EXISTS (SELECT FROM {EntityMapper.TbName<TreatmentApprovalDal>()}
                            WHERE {EntityMapper.ColName<TreatmentApprovalDal>(x => x.TreatmentId)} = {EntityMapper.ParameterName(nameof(id))}
                            AND {EntityMapper.ColName<TreatmentApprovalDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(approved))})
                        """;
                    TreatmentStateRepository.RequireTreatmentState(await _connection.ExecuteScalarAsync<bool>(approvalQuery, new { id, approved }, transaction));
                }
            }
            else
            {
                TreatmentStateRepository.RequireTreatmentState(measure.Status is TreatmentRules.Waiting or TreatmentRules.Working or TreatmentRules.Hold);
                await _jiraStateRepository.RequireJiraManualControlAsync(measure, transaction);
            }

            var previous = await _treatmentStateRepository.ReadAssignmentAsync(id, transaction);
            var operationId = Guid.NewGuid();
            if (request.AssigneeId is Guid assigneeId)
            {
                var candidates = await _treatmentStateRepository.ReadEligibleExecutorsAsync(measure.ProcessId, assigneeId, 0, 1, transaction);
                if (candidates.Length == 0)
                {
                    throw new IdentityCommandException(409, TreatmentRules.InvalidExecutor);
                }
                if (!dispatch && previous?.AssigneeId == assigneeId)
                {
                    return measure;
                }
                var row = new TreatmentAssignmentDal
                {
                    Id = Guid.NewGuid(), TreatmentId = id, AssigneeId = assigneeId,
                    AssignedById = actor.UserId, AssignmentSource = TreatmentRules.Manual
                };
                var query = $"""
                    INSERT INTO {EntityMapper.TbName<TreatmentAssignmentDal>()}
                        ({EntityMapper.ColName<TreatmentAssignmentDal>(x => x.Id)}, {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.TreatmentId)}, {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssigneeId)}, {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignedById)},
                         {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignmentSource)})
                    VALUES ({EntityMapper.ParameterName(nameof(TreatmentAssignmentDal.Id))}, {EntityMapper.ParameterName(nameof(TreatmentAssignmentDal.TreatmentId))}, {EntityMapper.ParameterName(nameof(TreatmentAssignmentDal.AssigneeId))}, {EntityMapper.ParameterName(nameof(TreatmentAssignmentDal.AssignedById))},
                            {EntityMapper.ParameterName(nameof(TreatmentAssignmentDal.AssignmentSource))})
                    ON CONFLICT ({EntityMapper.ColName<TreatmentAssignmentDal>(x => x.TreatmentId)}) DO UPDATE SET
                      {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssigneeId)} = EXCLUDED.{EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssigneeId)},
                      {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignedById)} = EXCLUDED.{EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignedById)},
                      {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignmentSource)} = EXCLUDED.{EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignmentSource)},
                      {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignedAt)} = clock_timestamp() RETURNING *
                    """;
                var assignment = await _connection.QuerySingleAsync<TreatmentAssignmentDal>(query, row, transaction);
                await BusinessHistory.RecordAsync(transaction, operationId, actor.UserId, assignment.Id, previous, assignment);
            }
            else
            {
                TreatmentStateRepository.RequireTreatmentState(!dispatch && measure.Status == TreatmentRules.Hold);
                if (previous is null)
                {
                    return measure;
                }
                var query = $"""
                    DELETE FROM {EntityMapper.TbName<TreatmentAssignmentDal>()} WHERE {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.TreatmentId)} = {EntityMapper.ParameterName(nameof(id))}
                    """;
                await _connection.ExecuteAsync(query, new { id }, transaction);
                await BusinessHistory.RecordAsync(transaction, operationId, actor.UserId, previous.Id, previous, null);
            }

            return await _treatmentStateRepository.SaveTreatmentStatusAsync(measure, dispatch ? TreatmentRules.Waiting : measure.Status,
                actor.UserId, operationId, transaction);
        });
    }

    /// <inheritdoc/>
    public async Task<TreatmentExecutorOption[]> ListTreatmentExecutorsAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        await RequireRightAsync(actor, TreatmentRules.Assign, transaction);
        var measure = await ReadAsync<TreatmentDal>(id, EntityMapper.ColName<TreatmentDal>(x => x.Id), transaction);
        await RequireTreatmentOwnerAsync(actor, measure, transaction);
        var rows = await _treatmentStateRepository.ReadEligibleExecutorsAsync(measure.ProcessId, null, offset, limit, transaction);
        transaction.Commit();
        return rows;
    }

    /// <inheritdoc/>
    public async Task<TreatmentCommentDal[]> ListTreatmentCommentsAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        await ReadAsync<TreatmentDal>(id, EntityMapper.ColName<TreatmentDal>(x => x.Id), transaction);
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<TreatmentCommentDal>()} WHERE {EntityMapper.ColName<TreatmentCommentDal>(x => x.TreatmentId)} = {EntityMapper.ParameterName(nameof(id))} AND NOT {EntityMapper.ColName<TreatmentCommentDal>(x => x.IsDeleted)}
            ORDER BY {EntityMapper.ColName<TreatmentCommentDal>(x => x.CreatedAt)}, {EntityMapper.ColName<TreatmentCommentDal>(x => x.Id)} OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var rows = await _connection.QueryAsync<TreatmentCommentDal>(query, new { id, offset, limit }, transaction);
        transaction.Commit();
        return rows.ToArray();
    }

    /// <inheritdoc/>
    public Task<TreatmentCommentDal> SaveTreatmentCommentAsync(RiskActor actor, Guid id, Guid? commentId,
        TreatmentCommentRequest request, bool delete)
    {
        return WriteAsync(actor, TreatmentRules.Comment, async transaction =>
        {
            await RequireRightAsync(actor, RiskRules.View, transaction);
            await ReadAsync<TreatmentDal>(id, EntityMapper.ColName<TreatmentDal>(x => x.Id), transaction);
            var authorId = actor.UserId;
            TreatmentCommentDal? before = null;
            if (commentId.HasValue)
            {
                before = await ReadAsync<TreatmentCommentDal>(commentId.Value, EntityMapper.ColName<TreatmentCommentDal>(x => x.Id), transaction);
                if (before.IsDeleted || before.TreatmentId != id)
                {
                    throw new IdentityCommandException(404, RiskRules.NotFound);
                }
                if (before.AuthorId != authorId && !await HasRightAsync(actor, IdentityRights.AdminCode, transaction))
                {
                    throw new IdentityCommandException(403, IdentityErrorCodes.AccessDenied);
                }
                RequireVersion(before.Version, request.Version);
            }
            if (delete && before is null)
            {
                throw new IdentityCommandException(400, TreatmentRules.Invalid);
            }
            var text = delete ? before?.Text : RequiredText(request.Text);
            var row = new TreatmentCommentDal
            {
                Id = before?.Id ?? Guid.NewGuid(), TreatmentId = id, AuthorId = authorId,
                Text = text ?? string.Empty, Version = request.Version
            };
            var insertQuery = $"""
                INSERT INTO {EntityMapper.TbName<TreatmentCommentDal>()}
                    ({EntityMapper.ColName<TreatmentCommentDal>(x => x.Id)}, {EntityMapper.ColName<TreatmentCommentDal>(x => x.TreatmentId)}, {EntityMapper.ColName<TreatmentCommentDal>(x => x.AuthorId)}, {EntityMapper.ColName<TreatmentCommentDal>(x => x.Text)})
                VALUES ({EntityMapper.ParameterName(nameof(TreatmentCommentDal.Id))}, {EntityMapper.ParameterName(nameof(TreatmentCommentDal.TreatmentId))}, {EntityMapper.ParameterName(nameof(TreatmentCommentDal.AuthorId))}, {EntityMapper.ParameterName(nameof(TreatmentCommentDal.Text))}) RETURNING *
                """;
            var updateQuery = $"""
                UPDATE {EntityMapper.TbName<TreatmentCommentDal>()} SET {EntityMapper.ColName<TreatmentCommentDal>(x => x.Text)} = {EntityMapper.ParameterName(nameof(row.Text))},
                    {EntityMapper.ColName<TreatmentCommentDal>(x => x.UpdatedAt)} = CASE WHEN {EntityMapper.ParameterName(nameof(delete))} THEN {EntityMapper.ColName<TreatmentCommentDal>(x => x.UpdatedAt)} ELSE clock_timestamp() END,
                    {EntityMapper.ColName<TreatmentCommentDal>(x => x.IsDeleted)} = {EntityMapper.ParameterName(nameof(delete))}, {EntityMapper.ColName<TreatmentCommentDal>(x => x.Version)} = gen_random_uuid(),
                    {EntityMapper.ColName<TreatmentCommentDal>(x => x.DeletedAt)} = CASE WHEN {EntityMapper.ParameterName(nameof(delete))} THEN clock_timestamp() ELSE NULL END,
                    {EntityMapper.ColName<TreatmentCommentDal>(x => x.DeletedById)} = CASE WHEN {EntityMapper.ParameterName(nameof(delete))} THEN {EntityMapper.ParameterName(nameof(authorId))} ELSE NULL END
                WHERE {EntityMapper.ColName<TreatmentCommentDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(row.Id))} AND {EntityMapper.ColName<TreatmentCommentDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(row.Version))} RETURNING *
                """;
            var saved = before is null
                ? await _connection.QuerySingleAsync<TreatmentCommentDal>(insertQuery, row, transaction)
                : await _connection.QuerySingleOrDefaultAsync<TreatmentCommentDal>(updateQuery,
                    new { row.Id, row.Version, row.Text, delete, authorId }, transaction)
                    ?? throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
            await BusinessHistory.RecordAsync(transaction, Guid.NewGuid(), actor.UserId, saved.Id, before, saved);
            return saved;
        });
    }

    /// <inheritdoc/>
    public async Task<TreatmentApprovalSettingsDal> GetTreatmentSettingsAsync(RiskActor actor)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, IdentityRights.AdminCode, transaction);
        var settings = await ReadTreatmentSettingsAsync(transaction);
        transaction.Commit();
        return settings;
    }

    /// <inheritdoc/>
    public Task<TreatmentApprovalSettingsDal> SaveTreatmentSettingsAsync(RiskActor actor, TreatmentThresholdsRequest request)
    {
        return WriteAsync(actor, IdentityRights.AdminCode, async transaction =>
        {
            var previous = await ReadTreatmentSettingsAsync(transaction);
            RequireVersion(previous.Version, request.Version);
            if (request.OwnerThreshold is not decimal owner || request.DirectorThreshold is not decimal director
                || request.CeoThreshold is not decimal ceo || owner >= director || director >= ceo)
            {
                throw new IdentityCommandException(400, TreatmentRules.Invalid);
            }
            ValidateTreatmentCost(owner);
            ValidateTreatmentCost(director);
            ValidateTreatmentCost(ceo);
            if (previous.OwnerThreshold == owner && previous.DirectorThreshold == director && previous.CeoThreshold == ceo)
            {
                return previous;
            }
            var query = $"""
                UPDATE {EntityMapper.TbName<TreatmentApprovalSettingsDal>()} SET {EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.OwnerThreshold)} = {EntityMapper.ParameterName(nameof(owner))},
                    {EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.DirectorThreshold)} = {EntityMapper.ParameterName(nameof(director))}, {EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.CeoThreshold)} = {EntityMapper.ParameterName(nameof(ceo))},
                    {EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.Version)} = gen_random_uuid()
                WHERE {EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(previous.Id))} AND {EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(previous.Version))} RETURNING *
                """;
            var settings = await _connection.QuerySingleOrDefaultAsync<TreatmentApprovalSettingsDal>(query,
                new { previous.Id, previous.Version, owner, director, ceo }, transaction)
                ?? throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
            await BusinessHistory.RecordAsync(transaction, Guid.NewGuid(), actor.UserId, settings.Id, previous, settings);
            return settings;
        });
    }

    /// <summary>Читает единственную действующую настройку клиента</summary>
    /// <param name="transaction">Согласованный снимок</param>
    /// <returns>Действующие пороги</returns>
    private Task<TreatmentApprovalSettingsDal> ReadTreatmentSettingsAsync(IDbTransaction transaction)
    {
        var id = Guid.Parse(Date_202611140500_AddTreatments.SettingsId);
        var query = $"SELECT * FROM {EntityMapper.TbName<TreatmentApprovalSettingsDal>()} WHERE {EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))}";
        return _connection.QuerySingleAsync<TreatmentApprovalSettingsDal>(query, new { id }, transaction);
    }

    /// <summary>Выбирает самый высокий достигнутый порог</summary>
    /// <param name="cost">Плановая стоимость</param>
    /// <param name="settings">Текущая настройка</param>
    /// <returns>Уровень согласования или null для прямой передачи</returns>
    private static string? ApprovalLevel(decimal cost, TreatmentApprovalSettingsDal settings)
    {
        if (cost >= settings.CeoThreshold)
        {
            return OrganizationRoles.Ceo;
        }
        if (cost >= settings.DirectorThreshold)
        {
            return OrganizationRoles.Director;
        }
        return cost >= settings.OwnerThreshold ? OrganizationRoles.Owner : null;
    }

    /// <inheritdoc/>
    public Task<TreatmentDal> TransitionTreatmentAsync(RiskActor actor, Guid id, string target, TreatmentActionRequest request)
    {
        return WriteAsync(actor, TreatmentRules.Execute, async transaction =>
        {
            await RequireRightAsync(actor, RiskRules.View, transaction);
            var measure = await ReadAsync<TreatmentDal>(id, EntityMapper.ColName<TreatmentDal>(x => x.Id), transaction);
            RequireVersion(measure.Version, request.Version);
            await RequireTreatmentOwnerAsync(actor, measure, transaction);
            await _jiraStateRepository.RequireJiraManualControlAsync(measure, transaction);
            var valid = target switch
            {
                TreatmentRules.Hold => measure.Status == TreatmentRules.Working,
                TreatmentRules.Working => measure.Status == TreatmentRules.Hold,
                TreatmentRules.Done => measure.Status is TreatmentRules.Working or TreatmentRules.Hold,
                TreatmentRules.Cancelled => measure.Status is TreatmentRules.Draft or TreatmentRules.Waiting or TreatmentRules.Working or TreatmentRules.Hold,
                _ => false
            };
            TreatmentStateRepository.RequireTreatmentState(valid);
            if (request.CostActual is not null && target != TreatmentRules.Done)
            {
                throw new IdentityCommandException(400, TreatmentRules.Invalid);
            }
            ValidateTreatmentCost(request.CostActual);
            if (target == TreatmentRules.Working)
            {
                await _treatmentStateRepository.RequireTreatmentCycleAsync(measure.RiskId, RiskRules.InProgress, transaction);
                await _treatmentStateRepository.SelectTreatmentLinkAsync(measure.RiskId, measure.RiskProcessId, transaction);
                await _treatmentStateRepository.RequireValidAssignmentAsync(measure, transaction);
            }
            var operationId = Guid.NewGuid();
            var updated = await _treatmentStateRepository.SaveTreatmentStatusAsync(measure, target, actor.UserId, operationId, transaction, request.CostActual);
            if (target == TreatmentRules.Working)
            {
                await _jiraStateRepository.QueueJiraAsync(measure.Id, actor.UserId, transaction, triggeredFromRisk: false);
            }
            if (target is TreatmentRules.Done or TreatmentRules.Cancelled)
            {
                await _treatmentLifecycleRepository.StartReviewAfterTreatmentAsync(measure, request.Confirmed, actor.UserId, operationId, transaction);
            }
            return updated;
        });
    }

    /// <inheritdoc/>
    public Task<RiskDal> StartTreatmentCycleAsync(RiskActor actor, Guid riskId, TreatmentActionRequest request)
    {
        return WriteAsync(actor, RiskRules.Edit, async transaction =>
        {
            await _riskStateRepository.RequirePrimaryOwnerAsync(actor, riskId, transaction);
            var risk = await _treatmentStateRepository.RequireTreatmentCycleAsync(riskId, RiskRules.Active, transaction);
            RequireVersion(risk.Version, request.Version);
            var active = TreatmentRules.ActiveStatuses();
            var query = $"""
                SELECT * FROM {EntityMapper.TbName<TreatmentDal>()} WHERE {EntityMapper.ColName<TreatmentDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(riskId))}
                    AND {EntityMapper.ColName<TreatmentDal>(x => x.Status)} = ANY({EntityMapper.ParameterName(nameof(active))}) ORDER BY {EntityMapper.ColName<TreatmentDal>(x => x.Id)}
                """;
            var measures = (await _connection.QueryAsync<TreatmentDal>(query, new { riskId, active }, transaction)).ToArray();
            TreatmentStateRepository.RequireTreatmentState(measures.Length > 0 && measures.All(x => x.Status == TreatmentRules.Waiting));
            var operationId = Guid.NewGuid();
            foreach (var measure in measures)
            {
                await _treatmentStateRepository.SelectTreatmentLinkAsync(riskId, measure.RiskProcessId, transaction);
                await _treatmentStateRepository.RequireValidAssignmentAsync(measure, transaction);
                await _treatmentStateRepository.SaveTreatmentStatusAsync(measure, TreatmentRules.Working, actor.UserId, operationId, transaction);
                await _jiraStateRepository.QueueJiraAsync(measure.Id, actor.UserId, transaction, triggeredFromRisk: true);
            }

            var updated = await _riskAssessmentStateRepository.SaveRiskStateAsync(transaction, risk, RiskRules.InProgress, risk.AcceptanceLevel);
            await BusinessHistory.RecordAsync(transaction, operationId, actor.UserId, riskId, risk, updated);
            return updated;
        });
    }

    /// <inheritdoc/>
    public async Task<TreatmentRegistry> ListTreatmentRegistryAsync(RiskActor actor, int offset, int limit)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        var fetchLimit = limit + 1;
        var pageQuery = $"""
            SELECT * FROM {EntityMapper.TbName<TreatmentDal>()}
            ORDER BY {EntityMapper.ColName<TreatmentDal>(x => x.CreatedAt)} DESC,
                {EntityMapper.ColName<TreatmentDal>(x => x.Id)} DESC
            OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(fetchLimit))}
            """;
        var rows = (await _connection.QueryAsync<TreatmentDal>(pageQuery, new { offset, fetchLimit }, transaction)).ToArray();
        var items = rows.Take(limit).ToArray();
        var riskNames = new Dictionary<Guid, string>();
        var processNames = new Dictionary<Guid, string>();
        if (items.Length > 0)
        {
            var riskIds = items.Select(item => item.RiskId).Distinct().ToArray();
            var riskQuery = $"""
                SELECT {EntityMapper.ColName<RiskDal>(x => x.Id)}, {EntityMapper.ColName<RiskDal>(x => x.Name)}
                FROM {EntityMapper.TbName<RiskDal>()}
                WHERE {EntityMapper.ColName<RiskDal>(x => x.Id)} = ANY({EntityMapper.ParameterName(nameof(riskIds))})
                """;
            var risks = await _connection.QueryAsync<RiskDal>(riskQuery, new { riskIds }, transaction);
            riskNames = risks.ToDictionary(risk => risk.Id, risk => risk.Name);

            var processIds = items.Select(item => item.ProcessId).Distinct().ToArray();
            var processQuery = $"""
                SELECT {EntityMapper.ColName<ProcessDal>(x => x.Id)}, {EntityMapper.ColName<ProcessDal>(x => x.Name)}
                FROM {EntityMapper.TbName<ProcessDal>()}
                WHERE {EntityMapper.ColName<ProcessDal>(x => x.Id)} = ANY({EntityMapper.ParameterName(nameof(processIds))})
                """;
            var processes = await _connection.QueryAsync<ProcessDal>(processQuery, new { processIds }, transaction);
            processNames = processes.ToDictionary(process => process.Id, process => process.Name);
        }
        transaction.Commit();
        return new TreatmentRegistry(items, rows.Length > limit) { RiskNames = riskNames, ProcessNames = processNames };
    }

    /// <inheritdoc/>
    public async Task<TreatmentDal[]> ListTreatmentsAsync(RiskActor actor, Guid riskId, int offset, int limit)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        await ReadAsync<RiskDal>(riskId, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<TreatmentDal>()} WHERE {EntityMapper.ColName<TreatmentDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(riskId))}
            ORDER BY {EntityMapper.ColName<TreatmentDal>(x => x.CreatedAt)}, {EntityMapper.ColName<TreatmentDal>(x => x.Id)} OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var rows = await _connection.QueryAsync<TreatmentDal>(query, new { riskId, offset, limit }, transaction);
        transaction.Commit();
        return rows.ToArray();
    }

    /// <inheritdoc/>
    public async Task<TreatmentCard> GetTreatmentAsync(RiskActor actor, Guid id)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        var measure = await ReadAsync<TreatmentDal>(id, EntityMapper.ColName<TreatmentDal>(x => x.Id), transaction);
        var assignment = await _treatmentStateRepository.ReadAssignmentAsync(id, transaction);
        string? assigneeName = null;
        if (assignment is not null)
        {
            var assigneeQuery = $"""
                SELECT profile.{EntityMapper.ColName<UserProfileDal>(x => x.FullName)}
                FROM {EntityMapper.TbName<OrgPersonDal>()} person
                JOIN {EntityMapper.TbName<UserProfileDal>()} profile
                    ON profile.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = person.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)}
                WHERE person.{EntityMapper.ColName<OrgPersonDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(assignment.AssigneeId))}
                """;
            assigneeName = await _connection.QuerySingleOrDefaultAsync<string>(assigneeQuery, new { assignment.AssigneeId }, transaction);
        }

        var query = $"""
            SELECT * FROM {EntityMapper.TbName<TreatmentApprovalDal>()} WHERE {EntityMapper.ColName<TreatmentApprovalDal>(x => x.TreatmentId)} = {EntityMapper.ParameterName(nameof(id))}
            ORDER BY {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RequestedAt)} DESC, {EntityMapper.ColName<TreatmentApprovalDal>(x => x.Id)} DESC LIMIT 1
            """;
        var approval = await _connection.QuerySingleOrDefaultAsync<TreatmentApprovalDal>(query, new { id }, transaction);
        var requiredLevel = measure.Status == TreatmentRules.Draft
            ? ApprovalLevel(measure.CostPlanned, await ReadTreatmentSettingsAsync(transaction))
            : null;
        var jira = await _jiraStateRepository.ReadJiraViewAsync(measure, transaction, actor.UserId);
        transaction.Commit();
        return new TreatmentCard(measure, assignment, approval, requiredLevel) { Jira = jira, AssigneeName = assigneeName };
    }

    /// <inheritdoc/>
    public Task<TreatmentDal> SaveTreatmentDraftAsync(RiskActor actor, Guid id, bool create, TreatmentDraftRequest request)
    {
        return WriteAsync(actor, create ? TreatmentRules.Create : TreatmentRules.Edit, async transaction =>
        {
            await RequireRightAsync(actor, RiskRules.View, transaction);
            TreatmentDal? before = null;
            var riskId = id;
            if (!create)
            {
                before = await ReadAsync<TreatmentDal>(id, EntityMapper.ColName<TreatmentDal>(x => x.Id), transaction);
                RequireVersion(before.Version, request.Version);
                TreatmentStateRepository.RequireTreatmentState(before.Status == TreatmentRules.Draft);
                await RequireTreatmentOwnerAsync(actor, before, transaction);
                riskId = before.RiskId;
            }
            else
            {
                await _treatmentStateRepository.RequireTreatmentCycleAsync(riskId, RiskRules.Active, transaction);
            }

            var link = await _treatmentStateRepository.SelectTreatmentLinkAsync(riskId, request.RiskProcessId ?? before?.RiskProcessId, transaction);
            if (request.DueDate == default || request.CostPlanned is not decimal planned)
            {
                throw new IdentityCommandException(400, TreatmentRules.Invalid);
            }
            ValidateTreatmentCost(planned);
            ValidateTreatmentCost(request.CostActual);
            var row = new TreatmentDal
            {
                Id = before?.Id ?? Guid.NewGuid(), Version = request.Version, RiskId = riskId, ProcessId = link.ProcessId,
                RiskProcessId = link.Id, Title = RequiredText(request.Title), Description = request.Description?.Trim(),
                DueDate = request.DueDate.ToDateTime(TimeOnly.MinValue), CostPlanned = planned, CostActual = request.CostActual,
                Status = TreatmentRules.Draft, CreatedById = actor.UserId
            };
            var insertQuery = $"""
                INSERT INTO {EntityMapper.TbName<TreatmentDal>()}
                ({EntityMapper.ColName<TreatmentDal>(x => x.Id)}, {EntityMapper.ColName<TreatmentDal>(x => x.RiskId)}, {EntityMapper.ColName<TreatmentDal>(x => x.ProcessId)}, {EntityMapper.ColName<TreatmentDal>(x => x.RiskProcessId)},
                     {EntityMapper.ColName<TreatmentDal>(x => x.Title)}, {EntityMapper.ColName<TreatmentDal>(x => x.Description)}, {EntityMapper.ColName<TreatmentDal>(x => x.DueDate)}, {EntityMapper.ColName<TreatmentDal>(x => x.CostPlanned)},
                     {EntityMapper.ColName<TreatmentDal>(x => x.CostActual)}, {EntityMapper.ColName<TreatmentDal>(x => x.Status)}, {EntityMapper.ColName<TreatmentDal>(x => x.CreatedById)})
                VALUES ({EntityMapper.ParameterName(nameof(TreatmentDal.Id))}, {EntityMapper.ParameterName(nameof(TreatmentDal.RiskId))}, {EntityMapper.ParameterName(nameof(TreatmentDal.ProcessId))}, {EntityMapper.ParameterName(nameof(TreatmentDal.RiskProcessId))},
                        {EntityMapper.ParameterName(nameof(TreatmentDal.Title))}, {EntityMapper.ParameterName(nameof(TreatmentDal.Description))}, {EntityMapper.ParameterName(nameof(TreatmentDal.DueDate))}, {EntityMapper.ParameterName(nameof(TreatmentDal.CostPlanned))},
                        {EntityMapper.ParameterName(nameof(TreatmentDal.CostActual))}, {EntityMapper.ParameterName(nameof(TreatmentDal.Status))}, {EntityMapper.ParameterName(nameof(TreatmentDal.CreatedById))}) RETURNING *
                """;
            var updateQuery = $"""
                UPDATE {EntityMapper.TbName<TreatmentDal>()} SET {EntityMapper.ColName<TreatmentDal>(x => x.ProcessId)} = {EntityMapper.ParameterName(nameof(row.ProcessId))}, {EntityMapper.ColName<TreatmentDal>(x => x.RiskProcessId)} = {EntityMapper.ParameterName(nameof(row.RiskProcessId))},
                    {EntityMapper.ColName<TreatmentDal>(x => x.Title)} = {EntityMapper.ParameterName(nameof(row.Title))}, {EntityMapper.ColName<TreatmentDal>(x => x.Description)} = {EntityMapper.ParameterName(nameof(row.Description))}, {EntityMapper.ColName<TreatmentDal>(x => x.DueDate)} = {EntityMapper.ParameterName(nameof(row.DueDate))},
                    {EntityMapper.ColName<TreatmentDal>(x => x.CostPlanned)} = {EntityMapper.ParameterName(nameof(row.CostPlanned))}, {EntityMapper.ColName<TreatmentDal>(x => x.CostActual)} = {EntityMapper.ParameterName(nameof(row.CostActual))},
                    {EntityMapper.ColName<TreatmentDal>(x => x.Version)} = gen_random_uuid(), {EntityMapper.ColName<TreatmentDal>(x => x.UpdatedAt)} = clock_timestamp()
                WHERE {EntityMapper.ColName<TreatmentDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(row.Id))} AND {EntityMapper.ColName<TreatmentDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(row.Version))}
                RETURNING *
                """;
            var saved = await _connection.QuerySingleOrDefaultAsync<TreatmentDal>(create ? insertQuery : updateQuery, row, transaction)
                ?? throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
            await BusinessHistory.RecordAsync(transaction, Guid.NewGuid(), actor.UserId, saved.Id, before, saved);
            return saved;
        });
    }

    /// <summary>Проверяет владение текущим процессом меры</summary>
    /// <param name="actor">Инициатор</param>
    /// <param name="measure">Мера</param>
    /// <param name="transaction">Транзакция операции</param>
    private async Task RequireTreatmentOwnerAsync(RiskActor actor, TreatmentDal measure, IDbTransaction transaction)
    {
        if (await HasRightAsync(actor, IdentityRights.AdminCode, transaction))
        {
            return;
        }
        var processId = measure.ProcessId;
        var userId = actor.UserId;
        var query = $"""
            SELECT EXISTS (SELECT FROM {EntityMapper.TbName<ProcessDal>()} p JOIN {EntityMapper.TbName<OrgPersonDal>()} o
              ON o.{EntityMapper.ColName<OrgPersonDal>(x => x.Id)} = p.{EntityMapper.ColName<ProcessDal>(x => x.OwnerId)} AND o.{EntityMapper.ColName<OrgPersonDal>(x => x.IsActive)}
              WHERE p.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(processId))} AND o.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)} = {EntityMapper.ParameterName(nameof(userId))})
            """;
        if (!await _connection.ExecuteScalarAsync<bool>(query, new { processId, userId }, transaction))
        {
            throw new IdentityCommandException(403, IdentityErrorCodes.AccessDenied);
        }
    }

    /// <summary>Валидирует необязательную стоимость в рублях</summary>
    /// <param name="amount">Введённая сумма</param>
    private static void ValidateTreatmentCost(decimal? amount)
    {
        if (amount is decimal value && (value < 0 || decimal.Round(value, 2) != value))
        {
            throw new IdentityCommandException(400, TreatmentRules.Invalid);
        }
    }
}
