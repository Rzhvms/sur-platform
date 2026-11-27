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
using Infrastructure.Repositories.Sessions.Interfaces;
using Infrastructure.Repositories.Shared;
using Infrastructure.Repositories.Treatments.Interfaces;
using Npgsql;
using System.Data;

namespace Infrastructure.Repositories.Treatments;

/// <summary>Меры, исполнители и согласования</summary>
internal sealed class TreatmentStateRepository : ClientRepository, ITreatmentStateRepository
{
    /// <summary>Текущая оценка и версии состояния риска</summary>
    private readonly IRiskAssessmentStateRepository _riskAssessmentStateRepository;

    /// <summary>Инициализирует операции выбранной клиентской базы</summary>
    /// <param name="connection">Подключение выбранной клиентской базы</param>
    /// <param name="sessions">Проверка сессии и эффективных прав в транзакции</param>
    /// <param name="operations">Метрики операций базы</param>
    /// <param name="riskAssessmentStateRepository">Текущая оценка и версии состояния риска</param>
    public TreatmentStateRepository(
        IDbConnection connection,
        ITransactionalSessionRepository sessions,
        OperationsMetrics operations,
        IRiskAssessmentStateRepository riskAssessmentStateRepository)
        : base(connection, sessions, operations)
    {
        _riskAssessmentStateRepository = riskAssessmentStateRepository;
    }

    /// <inheritdoc/>
    public Task<TreatmentAssignmentDal?> ReadAssignmentAsync(Guid id, IDbTransaction transaction)
    {
        var query = $"SELECT * FROM {EntityMapper.TbName<TreatmentAssignmentDal>()} WHERE {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.TreatmentId)} = {EntityMapper.ParameterName(nameof(id))}";
        return _connection.QuerySingleOrDefaultAsync<TreatmentAssignmentDal>(query, new { id }, transaction);
    }

    /// <inheritdoc/>
    public async Task RequireValidAssignmentAsync(TreatmentDal measure, IDbTransaction transaction)
    {
        var assignment = await ReadAssignmentAsync(measure.Id, transaction);
        if (assignment is null ||
            (await ReadEligibleExecutorsAsync(measure.ProcessId, assignment.AssigneeId, 0, 1, transaction)).Length != 1)
        {
            throw new IdentityCommandException(409, TreatmentRules.InvalidExecutor, [measure.Id]);
        }
    }

    /// <inheritdoc/>
    public async Task<TreatmentExecutorOption[]> ReadEligibleExecutorsAsync(Guid processId, Guid? personId,
        int offset, int limit, IDbTransaction transaction)
    {
        var roles = new[] { OrganizationRoles.Owner, TreatmentRules.Employee };
        var query = $"""
            SELECT p.{EntityMapper.ColName<OrgPersonDal>(x => x.Id)} AS {EntityMapper.ColName<TreatmentExecutorOption>(x => x.PersonId)},
                u.{EntityMapper.ColName<UserProfileDal>(x => x.FullName)} AS {EntityMapper.ColName<TreatmentExecutorOption>(x => x.FullName)}
            FROM {EntityMapper.TbName<ProcessDal>()} pr JOIN {EntityMapper.TbName<OrgPersonDal>()} p ON p.{EntityMapper.ColName<OrgPersonDal>(x => x.DirectorateId)} = pr.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)}
            JOIN {EntityMapper.TbName<UserProfileDal>()} u ON u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = p.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)}
            WHERE pr.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(processId))} AND p.{EntityMapper.ColName<OrgPersonDal>(x => x.IsActive)} AND u.{EntityMapper.ColName<UserProfileDal>(x => x.IsActive)}
              AND (CAST({EntityMapper.ParameterName(nameof(personId))} AS uuid) IS NULL OR p.{EntityMapper.ColName<OrgPersonDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(personId))})
              AND EXISTS (SELECT FROM {EntityMapper.TbName<UserRoleDal>()} ur JOIN {EntityMapper.TbName<RoleDal>()} r ON r.{EntityMapper.ColName<RoleDal>(x => x.Id)} = ur.{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}
                  WHERE ur.{EntityMapper.ColName<UserRoleDal>(x => x.UserId)} = p.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)} AND ur.{EntityMapper.ColName<UserRoleDal>(x => x.IsActive)}
                    AND r.{EntityMapper.ColName<RoleDal>(x => x.IsActive)} AND r.{EntityMapper.ColName<RoleDal>(x => x.Code)} = ANY({EntityMapper.ParameterName(nameof(roles))}))
            ORDER BY u.{EntityMapper.ColName<UserProfileDal>(x => x.FullName)}, p.{EntityMapper.ColName<OrgPersonDal>(x => x.Id)} OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var rows = await _connection.QueryAsync<TreatmentExecutorOption>(query, new { processId, personId, roles, offset, limit }, transaction);
        return rows.ToArray();
    }

    /// <inheritdoc/>
    public async Task<TreatmentDal> SaveTreatmentStatusAsync(TreatmentDal measure, string status, Guid actor, Guid operationId,
        IDbTransaction transaction, decimal? cost = null, JiraSettingsDal? jira = null)
    {
        var slots = TreatmentRules.SlotStatuses();
        if (slots.Contains(status))
        {
            var slotQuery = $"""
                SELECT EXISTS (SELECT FROM {EntityMapper.TbName<TreatmentDal>()} WHERE {EntityMapper.ColName<TreatmentDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(measure.RiskId))}
                    AND {EntityMapper.ColName<TreatmentDal>(x => x.ProcessId)} = {EntityMapper.ParameterName(nameof(measure.ProcessId))} AND {EntityMapper.ColName<TreatmentDal>(x => x.Id)} <> {EntityMapper.ParameterName(nameof(measure.Id))}
                    AND {EntityMapper.ColName<TreatmentDal>(x => x.Status)} = ANY({EntityMapper.ParameterName(nameof(slots))}))
                """;
            if (await _connection.ExecuteScalarAsync<bool>(slotQuery, new { measure.RiskId, measure.ProcessId, measure.Id, slots }, transaction))
            {
                throw new IdentityCommandException(409, TreatmentRules.SlotTaken);
            }
        }
        var query = $"""
            UPDATE {EntityMapper.TbName<TreatmentDal>()} SET {EntityMapper.ColName<TreatmentDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(status))},
                {EntityMapper.ColName<TreatmentDal>(x => x.CostActual)} = COALESCE({EntityMapper.ParameterName(nameof(cost))}, {EntityMapper.ColName<TreatmentDal>(x => x.CostActual)}),
                {EntityMapper.ColName<TreatmentDal>(x => x.Version)} = gen_random_uuid(), {EntityMapper.ColName<TreatmentDal>(x => x.UpdatedAt)} = clock_timestamp()
            WHERE {EntityMapper.ColName<TreatmentDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(measure.Id))} AND {EntityMapper.ColName<TreatmentDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(measure.Version))} RETURNING *
            """;
        TreatmentDal updated;
        try
        {
            updated = await _connection.QuerySingleOrDefaultAsync<TreatmentDal>(query,
                new { measure.Id, measure.Version, status, cost }, transaction)
                ?? throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.UniqueViolation &&
            error.ConstraintName == Date_202611140500_AddTreatments.SlotIndex)
        {
            throw new IdentityCommandException(409, TreatmentRules.SlotTaken);
        }
        await BusinessHistory.RecordAsync(transaction, operationId, jira?.ResponsibleUserId ?? actor, measure.Id, measure, updated,
            system: jira is not null, automaticSource: jira is null ? null : JiraRules.Source, configurationAuthorId: jira?.UpdatedById);
        return updated;
    }

    /// <inheritdoc/>
    public async Task RequireNoBlockingTreatmentsAsync(IDbTransaction transaction, Guid riskId, Guid[]? excludedProcesses = null, bool includeDrafts = false)
    {
        string[] statuses = excludedProcesses is null && !includeDrafts ? TreatmentRules.ActiveStatuses() : [..TreatmentRules.ActiveStatuses(), TreatmentRules.Draft];
        var query = $"""
            SELECT {EntityMapper.ColName<TreatmentDal>(x => x.Id)} FROM {EntityMapper.TbName<TreatmentDal>()} WHERE {EntityMapper.ColName<TreatmentDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(riskId))}
                AND {EntityMapper.ColName<TreatmentDal>(x => x.Status)} = ANY({EntityMapper.ParameterName(nameof(statuses))})
                AND (CAST({EntityMapper.ParameterName(nameof(excludedProcesses))} AS uuid[]) IS NULL OR {EntityMapper.ColName<TreatmentDal>(x => x.ProcessId)} = ANY({EntityMapper.ParameterName(nameof(excludedProcesses))}))
            """;
        var ids = (await _connection.QueryAsync<Guid>(query, new { riskId, statuses, excludedProcesses }, transaction)).ToArray();
        if (ids.Length > 0)
        {
            throw new IdentityCommandException(409, TreatmentRules.Conflict, ids);
        }
    }

    /// <inheritdoc/>
    public async Task<RiskProcessDal> SelectTreatmentLinkAsync(Guid riskId, Guid? linkId, IDbTransaction transaction)
    {
        var query = $"""
            SELECT l.* FROM {EntityMapper.TbName<RiskProcessDal>()} l JOIN {EntityMapper.TbName<ProcessDal>()} p ON p.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = l.{EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)}
            WHERE l.{EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(riskId))} AND l.{EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL AND p.{EntityMapper.ColName<ProcessDal>(x => x.IsActive)}
              AND (CAST({EntityMapper.ParameterName(nameof(linkId))} AS uuid) IS NULL OR l.{EntityMapper.ColName<RiskProcessDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(linkId))}) LIMIT 2
            """;
        var rows = (await _connection.QueryAsync<RiskProcessDal>(query, new { riskId, linkId }, transaction)).ToArray();
        RequireTreatmentState(rows.Length == 1);
        return rows[0];
    }

    /// <inheritdoc/>
    public async Task<RiskDal> RequireTreatmentCycleAsync(Guid riskId, string status, IDbTransaction transaction)
    {
        var risk = await ReadAsync<RiskDal>(riskId, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
        RequireTreatmentState(risk.Status == status);
        var assessment = await _riskAssessmentStateRepository.ReadCurrentAssessmentAsync(riskId, transaction);
        RequireTreatmentState(assessment?.ResponseMode is RiskRules.TreatMode or RiskRules.SplitMode);
        return risk;
    }

    /// <summary>Отклоняет команду при нарушении условий жизненного цикла</summary>
    /// <param name="valid">Выполнены ли условия</param>
    public static void RequireTreatmentState(bool valid)
    {
        if (!valid)
        {
            throw new IdentityCommandException(409, TreatmentRules.Conflict);
        }
    }
}
