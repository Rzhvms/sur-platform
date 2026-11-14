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

/// <summary>Карточка меры и редактирование черновика</summary>
internal sealed partial class RiskRepository
{
    /// <inheritdoc/>
    public async Task<TreatmentDal[]> ListTreatmentsAsync(RiskActor actor, Guid riskId, int offset, int limit)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        await ReadAsync<RiskDal>(riskId, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<TreatmentDal>()} WHERE {EntityMapper.ColName<TreatmentDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(riskId))}
            ORDER BY {EntityMapper.ColName<TreatmentDal>(x => x.CreatedAt)}, {EntityMapper.ColName<TreatmentDal>(x => x.Id)} OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var rows = await connection.QueryAsync<TreatmentDal>(query, new { riskId, offset, limit }, transaction);
        transaction.Commit();
        return rows.ToArray();
    }

    /// <inheritdoc/>
    public async Task<TreatmentCard> GetTreatmentAsync(RiskActor actor, Guid id)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        var measure = await ReadAsync<TreatmentDal>(id, EntityMapper.ColName<TreatmentDal>(x => x.Id), transaction);
        var assignment = await ReadAssignmentAsync(id, transaction);
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<TreatmentApprovalDal>()} WHERE {EntityMapper.ColName<TreatmentApprovalDal>(x => x.TreatmentId)} = {EntityMapper.ParameterName(nameof(id))}
            ORDER BY {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RequestedAt)} DESC, {EntityMapper.ColName<TreatmentApprovalDal>(x => x.Id)} DESC LIMIT 1
            """;
        var approval = await connection.QuerySingleOrDefaultAsync<TreatmentApprovalDal>(query, new { id }, transaction);
        var requiredLevel = measure.Status == TreatmentRules.Draft
            ? ApprovalLevel(measure.CostPlanned, await ReadTreatmentSettingsAsync(transaction))
            : null;
        var jira = await ReadJiraViewAsync(measure, transaction);
        transaction.Commit();
        return new TreatmentCard(measure, assignment, approval, requiredLevel) { Jira = jira };
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
                RequireTreatmentState(before.Status == TreatmentRules.Draft);
                await RequireTreatmentOwnerAsync(actor, before, transaction);
                riskId = before.RiskId;
            }
            else
            {
                await RequireTreatmentCycleAsync(riskId, RiskRules.Active, transaction);
            }

            var link = await SelectTreatmentLinkAsync(riskId, request.RiskProcessId ?? before?.RiskProcessId, transaction);
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
            var saved = await connection.QuerySingleOrDefaultAsync<TreatmentDal>(create ? insertQuery : updateQuery, row, transaction)
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
        if (!await connection.ExecuteScalarAsync<bool>(query, new { processId, userId }, transaction))
        {
            throw new IdentityCommandException(403, IdentityErrorCodes.AccessDenied);
        }
    }

    /// <summary>Выбирает текущую связь строго из того же риска</summary>
    /// <param name="riskId">Риск</param>
    /// <param name="linkId">Выбранная связь или null для единственной</param>
    /// <param name="transaction">Транзакция операции</param>
    /// <returns>Подходящая активная связь</returns>
    private async Task<RiskProcessDal> SelectTreatmentLinkAsync(Guid riskId, Guid? linkId, IDbTransaction transaction)
    {
        var query = $"""
            SELECT l.* FROM {EntityMapper.TbName<RiskProcessDal>()} l JOIN {EntityMapper.TbName<ProcessDal>()} p ON p.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = l.{EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)}
            WHERE l.{EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(riskId))} AND l.{EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL AND p.{EntityMapper.ColName<ProcessDal>(x => x.IsActive)}
              AND (CAST({EntityMapper.ParameterName(nameof(linkId))} AS uuid) IS NULL OR l.{EntityMapper.ColName<RiskProcessDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(linkId))}) LIMIT 2
            """;
        var rows = (await connection.QueryAsync<RiskProcessDal>(query, new { riskId, linkId }, transaction)).ToArray();
        RequireTreatmentState(rows.Length == 1);
        return rows[0];
    }

    /// <summary>Проверяет оценку и режим цикла риска</summary>
    /// <param name="riskId">Риск</param>
    /// <param name="status">Требуемый статус риска</param>
    /// <param name="transaction">Транзакция операции</param>
    /// <returns>Карточка допустимого риска</returns>
    private async Task<RiskDal> RequireTreatmentCycleAsync(Guid riskId, string status, IDbTransaction transaction)
    {
        var risk = await ReadAsync<RiskDal>(riskId, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
        RequireTreatmentState(risk.Status == status);
        var assessment = await ReadCurrentAssessmentAsync(riskId, transaction);
        RequireTreatmentState(assessment?.ResponseMode is RiskRules.TreatMode or RiskRules.SplitMode);
        return risk;
    }

    /// <summary>Отклоняет команду при нарушении условий жизненного цикла</summary>
    /// <param name="valid">Выполнены ли условия</param>
    private static void RequireTreatmentState(bool valid)
    {
        if (!valid)
        {
            throw new IdentityCommandException(409, TreatmentRules.Conflict);
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
