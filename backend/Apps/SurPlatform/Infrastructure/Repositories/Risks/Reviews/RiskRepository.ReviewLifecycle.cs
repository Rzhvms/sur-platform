using System.Data;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.UseCases.Risks.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Repositories.Risks.Helpers;

namespace Infrastructure.Repositories.Risks;

/// <summary>Открытие и завершение пересмотра в транзакции бизнес-команды</summary>
internal sealed partial class RiskRepository
{
    /// <summary>Сохраняет запуск с фиксированным источником и набором целей</summary>
    /// <param name="row">Снимок запуска</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    private async Task InsertReviewRunAsync(ReviewRunDal row, IDbTransaction transaction)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<ReviewRunDal>()}
                ({EntityMapper.ColName<ReviewRunDal>(x => x.Id)}, {EntityMapper.ColName<ReviewRunDal>(x => x.Reason)}, {EntityMapper.ColName<ReviewRunDal>(x => x.SourceType)}, {EntityMapper.ColName<ReviewRunDal>(x => x.SourceId)}, {EntityMapper.ColName<ReviewRunDal>(x => x.SourceNameSnapshot)}, {EntityMapper.ColName<ReviewRunDal>(x => x.ExternalSystemNameSnapshot)}, {EntityMapper.ColName<ReviewRunDal>(x => x.InitiatedById)}, {EntityMapper.ColName<ReviewRunDal>(x => x.ConfigurationAuthorId)}, {EntityMapper.ColName<ReviewRunDal>(x => x.ResponsibleUserId)}, {EntityMapper.ColName<ReviewRunDal>(x => x.TimeZone)}, {EntityMapper.ColName<ReviewRunDal>(x => x.ScheduledDate)}, {EntityMapper.ColName<ReviewRunDal>(x => x.OccurredAt)}, {EntityMapper.ColName<ReviewRunDal>(x => x.Status)}, {EntityMapper.ColName<ReviewRunDal>(x => x.RawPayload)}, {EntityMapper.ColName<ReviewRunDal>(x => x.TargetRiskIds)})
            VALUES ({EntityMapper.ParameterName(nameof(row.Id))}, {EntityMapper.ParameterName(nameof(row.Reason))}, {EntityMapper.ParameterName(nameof(row.SourceType))}, {EntityMapper.ParameterName(nameof(row.SourceId))}, {EntityMapper.ParameterName(nameof(row.SourceNameSnapshot))}, {EntityMapper.ParameterName(nameof(row.ExternalSystemNameSnapshot))}, {EntityMapper.ParameterName(nameof(row.InitiatedById))}, {EntityMapper.ParameterName(nameof(row.ConfigurationAuthorId))}, {EntityMapper.ParameterName(nameof(row.ResponsibleUserId))}, {EntityMapper.ParameterName(nameof(row.TimeZone))}, {EntityMapper.ParameterName(nameof(row.ScheduledDate))}, {EntityMapper.ParameterName(nameof(row.OccurredAt))}, {EntityMapper.ParameterName(nameof(row.Status))}, {EntityMapper.ParameterName(nameof(row.RawPayload))}, {EntityMapper.ParameterName(nameof(row.TargetRiskIds))})
            """;
        await connection.ExecuteAsync(query, row, transaction);
    }

    /// <summary>Открывает пересмотр для одного риска</summary>
    /// <param name="risk">Исходный риск</param>
    /// <param name="run">Сохранённый запуск</param>
    /// <param name="operationId">Связь записей общего журнала</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <param name="relatedChanges">Изменения процессов при передаче</param>
    /// <returns>Карточка в ожидании переоценки</returns>
    private async Task<RiskDal> OpenReviewAsync(RiskDal risk, ReviewRunDal run, Guid operationId, IDbTransaction transaction,
        List<(string Path, object? Before, object After)>? relatedChanges = null)
    {
        if (await ReadWaitingReviewAsync(risk.Id, transaction) is not null)
        {
            throw new IdentityCommandException(409, RiskRules.InvalidTransition);
        }

        var current = await ReadCurrentAssessmentAsync(risk.Id, transaction);
        var previous = current ?? await ReadLatestAssessmentAsync(risk.Id, transaction);
        var changes = relatedChanges ?? [];
        if (current is not null)
        {
            var mode = run.SourceType == ReviewRules.Transfer ? ReviewRules.TransferMode : current.ResponseMode;
            var historical = await SetAssessmentStateAsync(transaction, current, false, mode);
            changes.Add((BusinessHistory.AssessmentsPath, current, historical));
        }

        var updated = await SaveRiskStateAsync(transaction, risk, RiskRules.AwaitingReview, null, clearCloseReason: true);
        await InsertRiskReviewAsync(run.Id, risk.Id, ReviewRules.Waiting, transaction, previous?.Id, run.InitiatedById);
        var jira = run.ExternalSystemNameSnapshot == JiraRules.Source && run.Reason == ReviewRules.AfterTreatment;
        await BusinessHistory.RecordAsync(transaction, operationId, jira ? run.ResponsibleUserId ?? run.InitiatedById : run.InitiatedById,
            risk.Id, risk, updated, system: jira, relatedChanges: changes, automaticSource: jira ? JiraRules.Source : null,
            configurationAuthorId: jira ? run.ConfigurationAuthorId : null);
        return updated;
    }

    /// <summary>Создаёт и открывает однорисковый пересмотр</summary>
    /// <param name="risk">Исходный риск</param>
    /// <param name="reason">Основание запуска</param>
    /// <param name="source">Тип источника</param>
    /// <param name="sourceId">Идентификатор источника</param>
    /// <param name="actor">Инициатор бизнес-команды</param>
    /// <param name="operationId">Общий идентификатор журнала</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <param name="relatedChanges">Изменения процессов при передаче</param>
    /// <param name="jira">Атрибуция пересмотра из Jira</param>
    /// <returns>Карточка в ожидании переоценки</returns>
    private async Task<RiskDal> StartSingleReviewAsync(RiskDal risk, string reason, string source, Guid sourceId, Guid actor,
        Guid operationId, IDbTransaction transaction, List<(string Path, object? Before, object After)>? relatedChanges = null,
        JiraSettingsDal? jira = null)
    {
        var automatic = reason == ReviewRules.AfterTreatment;
        var run = new ReviewRunDal
        {
            Id = Guid.NewGuid(), Reason = reason, SourceType = source, SourceId = sourceId,
            InitiatedById = automatic ? IdentityAccounts.SystemUserId : actor,
            ResponsibleUserId = jira?.ResponsibleUserId ?? (automatic ? actor : null),
            ConfigurationAuthorId = jira?.UpdatedById, ExternalSystemNameSnapshot = jira is null ? null : JiraRules.Source,
            OccurredAt = DateTime.UtcNow,
            Status = ReviewRules.Completed, TargetRiskIds = [risk.Id]
        };
        await InsertReviewRunAsync(run, transaction);
        return await OpenReviewAsync(risk, run, operationId, transaction, relatedChanges);
    }

    /// <summary>Фиксирует результат первичной обработки</summary>
    /// <param name="runId">Первоначальный запуск</param>
    /// <param name="riskId">Целевой риск</param>
    /// <param name="status">Результат обработки</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <param name="previous">Последняя оценка</param>
    /// <param name="startedBy">Инициатор начала</param>
    private async Task InsertRiskReviewAsync(Guid runId, Guid riskId, string status, IDbTransaction transaction,
        Guid? previous = null, Guid? startedBy = null)
    {
        var now = DateTime.UtcNow;
        var row = new RiskReviewDal
        {
            Id = Guid.NewGuid(), ReviewRunId = runId, RiskId = riskId, Status = status,
            ProcessedAt = now, PreviousAssessmentId = previous, StartedById = startedBy,
            StartedAt = startedBy.HasValue ? now : null,
            ErrorMessage = status == ReviewRules.Error ? ReviewRules.ProcessingFailed : null
        };
        var query = $"""
            INSERT INTO {EntityMapper.TbName<RiskReviewDal>()}
                ({EntityMapper.ColName<RiskReviewDal>(x => x.Id)}, {EntityMapper.ColName<RiskReviewDal>(x => x.ReviewRunId)}, {EntityMapper.ColName<RiskReviewDal>(x => x.RiskId)}, {EntityMapper.ColName<RiskReviewDal>(x => x.Status)}, {EntityMapper.ColName<RiskReviewDal>(x => x.ProcessedAt)}, {EntityMapper.ColName<RiskReviewDal>(x => x.ErrorMessage)}, {EntityMapper.ColName<RiskReviewDal>(x => x.PreviousAssessmentId)}, {EntityMapper.ColName<RiskReviewDal>(x => x.StartedAt)}, {EntityMapper.ColName<RiskReviewDal>(x => x.StartedById)})
            VALUES ({EntityMapper.ParameterName(nameof(row.Id))}, {EntityMapper.ParameterName(nameof(row.ReviewRunId))}, {EntityMapper.ParameterName(nameof(row.RiskId))}, {EntityMapper.ParameterName(nameof(row.Status))}, {EntityMapper.ParameterName(nameof(row.ProcessedAt))}, {EntityMapper.ParameterName(nameof(row.ErrorMessage))}, {EntityMapper.ParameterName(nameof(row.PreviousAssessmentId))}, {EntityMapper.ParameterName(nameof(row.StartedAt))}, {EntityMapper.ParameterName(nameof(row.StartedById))})
            """;
        await connection.ExecuteAsync(query, row, transaction);
    }

    /// <summary>Читает единственный открытый пересмотр</summary>
    /// <param name="riskId">Риск</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <returns>Открытый пересмотр при наличии</returns>
    private Task<RiskReviewDal?> ReadWaitingReviewAsync(Guid riskId, IDbTransaction transaction)
    {
        var waiting = ReviewRules.Waiting;
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<RiskReviewDal>()} WHERE {EntityMapper.ColName<RiskReviewDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(riskId))} AND {EntityMapper.ColName<RiskReviewDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(waiting))}
            """;
        return connection.QuerySingleOrDefaultAsync<RiskReviewDal>(query, new { riskId, waiting }, transaction);
    }

    /// <summary>Читает последнюю оценку, включая историческую</summary>
    /// <param name="id">Риск</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <returns>Последняя оценка при наличии</returns>
    private Task<RiskAssessmentDal?> ReadLatestAssessmentAsync(Guid id, IDbTransaction transaction)
    {
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<RiskAssessmentDal>()} WHERE {EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(id))}
            ORDER BY {EntityMapper.ColName<RiskAssessmentDal>(x => x.CreatedAt)} DESC, {EntityMapper.ColName<RiskAssessmentDal>(x => x.Id)} DESC LIMIT 1
            """;
        return connection.QuerySingleOrDefaultAsync<RiskAssessmentDal>(query, new { id }, transaction);
    }

    /// <summary>Завершает открытый пересмотр без изменения первоначального запуска</summary>
    /// <param name="riskId">Риск</param>
    /// <param name="actor">Исполнитель</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <param name="assessmentId">Новая оценка</param>
    /// <param name="closeReason">Причина закрытия</param>
    private async Task CompleteReviewAsync(Guid riskId, Guid actor, IDbTransaction transaction, Guid? assessmentId, string? closeReason)
    {
        var completed = ReviewRules.Completed;
        var waiting = ReviewRules.Waiting;
        var query = $"""
            UPDATE {EntityMapper.TbName<RiskReviewDal>()} SET {EntityMapper.ColName<RiskReviewDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(completed))}, {EntityMapper.ColName<RiskReviewDal>(x => x.CompletedAt)} = clock_timestamp(),
                {EntityMapper.ColName<RiskReviewDal>(x => x.CompletedById)} = {EntityMapper.ParameterName(nameof(actor))}, {EntityMapper.ColName<RiskReviewDal>(x => x.NewAssessmentId)} = {EntityMapper.ParameterName(nameof(assessmentId))}, {EntityMapper.ColName<RiskReviewDal>(x => x.CloseReason)} = {EntityMapper.ParameterName(nameof(closeReason))}
            WHERE {EntityMapper.ColName<RiskReviewDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(riskId))} AND {EntityMapper.ColName<RiskReviewDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(waiting))}
            """;
        if (await connection.ExecuteAsync(query, new { riskId, actor, assessmentId, closeReason, completed, waiting }, transaction) != 1)
        {
            throw new IdentityCommandException(409, RiskRules.InvalidTransition);
        }
    }
}
