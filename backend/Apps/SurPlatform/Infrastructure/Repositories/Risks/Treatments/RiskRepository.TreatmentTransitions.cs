using System.Data;
using Npgsql;
using Infrastructure.Migrations;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.UseCases.Treatments.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Repositories.Risks.Helpers;

namespace Infrastructure.Repositories.Risks;

/// <summary>Ручные переходы и атомарный запуск выполнения риска</summary>
internal sealed partial class RiskRepository
{
    /// <inheritdoc/>
    public Task<TreatmentDal> TransitionTreatmentAsync(RiskActor actor, Guid id, string target, TreatmentActionRequest request)
    {
        return WriteAsync(actor, TreatmentRules.Execute, async transaction =>
        {
            await RequireRightAsync(actor, RiskRules.View, transaction);
            var measure = await ReadAsync<TreatmentDal>(id, EntityMapper.ColName<TreatmentDal>(x => x.Id), transaction);
            RequireVersion(measure.Version, request.Version);
            await RequireTreatmentOwnerAsync(actor, measure, transaction);
            await RequireJiraManualControlAsync(measure, transaction);
            var valid = target switch
            {
                TreatmentRules.Hold => measure.Status == TreatmentRules.Working,
                TreatmentRules.Working => measure.Status == TreatmentRules.Hold,
                TreatmentRules.Done => measure.Status is TreatmentRules.Working or TreatmentRules.Hold,
                TreatmentRules.Cancelled => measure.Status is TreatmentRules.Draft or TreatmentRules.Waiting or TreatmentRules.Working or TreatmentRules.Hold,
                _ => false
            };
            RequireTreatmentState(valid);
            if (request.CostActual is not null && target != TreatmentRules.Done)
            {
                throw new IdentityCommandException(400, TreatmentRules.Invalid);
            }
            ValidateTreatmentCost(request.CostActual);
            if (target == TreatmentRules.Working)
            {
                await RequireTreatmentCycleAsync(measure.RiskId, RiskRules.InProgress, transaction);
                await SelectTreatmentLinkAsync(measure.RiskId, measure.RiskProcessId, transaction);
                await RequireValidAssignmentAsync(measure, transaction);
            }
            var operationId = Guid.NewGuid();
            var updated = await SaveTreatmentStatusAsync(measure, target, actor.UserId, operationId, transaction, request.CostActual);
            if (target == TreatmentRules.Working)
            {
                await QueueJiraAsync(measure.Id, actor.UserId, transaction);
            }
            if (target is TreatmentRules.Done or TreatmentRules.Cancelled)
            {
                await StartReviewAfterTreatmentAsync(measure, request.Confirmed, actor.UserId, operationId, transaction);
            }
            return updated;
        });
    }

    /// <inheritdoc/>
    public Task<RiskDal> StartTreatmentCycleAsync(RiskActor actor, Guid riskId, TreatmentActionRequest request)
    {
        return WriteAsync(actor, RiskRules.Edit, async transaction =>
        {
            await RequirePrimaryOwnerAsync(actor, riskId, transaction);
            var risk = await RequireTreatmentCycleAsync(riskId, RiskRules.Active, transaction);
            RequireVersion(risk.Version, request.Version);
            var active = TreatmentRules.ActiveStatuses();
            var query = $"""
                SELECT * FROM {EntityMapper.TbName<TreatmentDal>()} WHERE {EntityMapper.ColName<TreatmentDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(riskId))}
                    AND {EntityMapper.ColName<TreatmentDal>(x => x.Status)} = ANY({EntityMapper.ParameterName(nameof(active))}) ORDER BY {EntityMapper.ColName<TreatmentDal>(x => x.Id)}
                """;
            var measures = (await connection.QueryAsync<TreatmentDal>(query, new { riskId, active }, transaction)).ToArray();
            RequireTreatmentState(measures.Length > 0 && measures.All(x => x.Status == TreatmentRules.Waiting));
            var operationId = Guid.NewGuid();
            foreach (var measure in measures)
            {
                await SelectTreatmentLinkAsync(riskId, measure.RiskProcessId, transaction);
                await RequireValidAssignmentAsync(measure, transaction);
                await SaveTreatmentStatusAsync(measure, TreatmentRules.Working, actor.UserId, operationId, transaction);
                await QueueJiraAsync(measure.Id, actor.UserId, transaction);
            }

            var updated = await SaveRiskStateAsync(transaction, risk, RiskRules.InProgress, risk.AcceptanceLevel);
            await BusinessHistory.RecordAsync(transaction, operationId, actor.UserId, riskId, risk, updated);
            return updated;
        });
    }

    /// <summary>Меняет статус меры, проверяет слот и ставит поручение журнала</summary>
    /// <param name="measure">Мера и ожидаемая версия</param>
    /// <param name="status">Целевой статус</param>
    /// <param name="actor">Инициатор действия</param>
    /// <param name="operationId">Общая операция журнала</param>
    /// <param name="transaction">Транзакция риск–мера</param>
    /// <param name="cost">Фактическая стоимость при завершении</param>
    /// <param name="jira">Атрибуция внешней операции</param>
    /// <returns>Обновлённая мера</returns>
    private async Task<TreatmentDal> SaveTreatmentStatusAsync(TreatmentDal measure, string status, Guid actor, Guid operationId,
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
            if (await connection.ExecuteScalarAsync<bool>(slotQuery, new { measure.RiskId, measure.ProcessId, measure.Id, slots }, transaction))
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
            updated = await connection.QuerySingleOrDefaultAsync<TreatmentDal>(query,
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

    /// <summary>Открывает пересмотр после последней активной меры</summary>
    /// <param name="measure">Исходное состояние меры</param>
    /// <param name="confirmed">Подтверждение при оставшихся черновиках</param>
    /// <param name="transaction">Транзакция перехода</param>
    /// <param name="actor">Пользователь исходной команды</param>
    /// <param name="operationId">Общее событие меры и риска</param>
    /// <param name="jira">Настройка внешней операции</param>
    private async Task StartReviewAfterTreatmentAsync(TreatmentDal measure, bool confirmed, Guid actor, Guid operationId,
        IDbTransaction transaction, JiraSettingsDal? jira = null)
    {
        if (measure.Status == TreatmentRules.Draft)
        {
            return;
        }
        var active = TreatmentRules.ActiveStatuses();
        var draft = TreatmentRules.Draft;
        var query = $"""
            SELECT {EntityMapper.ColName<TreatmentDal>(x => x.Status)} FROM {EntityMapper.TbName<TreatmentDal>()} WHERE {EntityMapper.ColName<TreatmentDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(measure.RiskId))}
                AND {EntityMapper.ColName<TreatmentDal>(x => x.Id)} <> {EntityMapper.ParameterName(nameof(measure.Id))}
                AND ({EntityMapper.ColName<TreatmentDal>(x => x.Status)} = ANY({EntityMapper.ParameterName(nameof(active))}) OR {EntityMapper.ColName<TreatmentDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(draft))})
            """;
        var remaining = (await connection.QueryAsync<string>(query, new { measure.RiskId, measure.Id, active, draft }, transaction)).ToArray();
        if (remaining.Any(status => active.Contains(status)))
        {
            return;
        }

        if (remaining.Length > 0 && !confirmed)
        {
            throw new IdentityCommandException(409, TreatmentRules.ConfirmationRequired);
        }

        var risk = await ReadAsync<RiskDal>(measure.RiskId, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
        RequireTreatmentState(risk.Status is RiskRules.Active or RiskRules.InProgress);
        await StartSingleReviewAsync(risk, ReviewRules.AfterTreatment, ReviewRules.Treatment, measure.Id, actor, operationId, transaction, jira: jira);
    }

    /// <summary>Запрещает решение по риску или удаление связи при блокирующих мерах</summary>
    /// <param name="transaction">Транзакция решения</param>
    /// <param name="riskId">Риск</param>
    /// <param name="excludedProcesses">Исключаемые процессы, null для принятия или закрытия</param>
    /// <param name="includeDrafts">Запретить и черновые меры при передаче</param>
    private async Task RequireNoBlockingTreatmentsAsync(IDbTransaction transaction, Guid riskId, Guid[]? excludedProcesses = null, bool includeDrafts = false)
    {
        string[] statuses = excludedProcesses is null && !includeDrafts ? TreatmentRules.ActiveStatuses() : [..TreatmentRules.ActiveStatuses(), TreatmentRules.Draft];
        var query = $"""
            SELECT {EntityMapper.ColName<TreatmentDal>(x => x.Id)} FROM {EntityMapper.TbName<TreatmentDal>()} WHERE {EntityMapper.ColName<TreatmentDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(riskId))}
                AND {EntityMapper.ColName<TreatmentDal>(x => x.Status)} = ANY({EntityMapper.ParameterName(nameof(statuses))})
                AND (CAST({EntityMapper.ParameterName(nameof(excludedProcesses))} AS uuid[]) IS NULL OR {EntityMapper.ColName<TreatmentDal>(x => x.ProcessId)} = ANY({EntityMapper.ParameterName(nameof(excludedProcesses))}))
            """;
        var ids = (await connection.QueryAsync<Guid>(query, new { riskId, statuses, excludedProcesses }, transaction)).ToArray();
        if (ids.Length > 0)
        {
            throw new IdentityCommandException(409, TreatmentRules.Conflict, ids);
        }
    }
}
