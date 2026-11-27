using Infrastructure.Repositories.History.Helpers;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.Ports.Repositories;
using Application.UseCases.Risks.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Observability;
using Infrastructure.Repositories.Assessments.Interfaces;
using Infrastructure.Repositories.Reviews.Interfaces;
using Infrastructure.Repositories.Sessions.Interfaces;
using Infrastructure.Repositories.Shared;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Domain.DalModels.Assessments;
using Domain.DalModels.Reviews;
using Domain.DalModels.Risks;
using Domain.DalModels.Users;

namespace Infrastructure.Repositories.Reviews;

/// <summary>Настройка и обработка пересмотров</summary>
internal sealed class ReviewStateRepository : ClientRepository, IReviewStateRepository
{
    /// <summary>Текущая оценка и версии состояния риска</summary>
    private readonly IRiskAssessmentStateRepository _riskAssessmentStateRepository;

    /// <summary>Инициализирует операции выбранной клиентской базы</summary>
    /// <param name="connection">Подключение выбранной клиентской базы</param>
    /// <param name="sessions">Проверка сессии и эффективных прав в транзакции</param>
    /// <param name="operations">Метрики операций базы</param>
    /// <param name="riskAssessmentStateRepository">Текущая оценка и версии состояния риска</param>
    public ReviewStateRepository(
        IDbConnection connection,
        ITransactionalSessionRepository sessions,
        OperationsMetrics operations,
        IRiskAssessmentStateRepository riskAssessmentStateRepository)
        : base(connection, sessions, operations)
    {
        _riskAssessmentStateRepository = riskAssessmentStateRepository;
    }

    /// <inheritdoc/>
    public async Task<RiskReviewView[]> ReadRiskReviewsAsync(Guid id, int offset, int limit, IDbTransaction transaction)
    {
        var waiting = ReviewRules.Waiting;
        var query = $"""
            SELECT rr.*, run.{EntityMapper.ColName<ReviewRunDal>(x => x.Id)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.Reason)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.SourceType)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.SourceId)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.SourceNameSnapshot)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.ExternalSystemNameSnapshot)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.InitiatedById)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.ConfigurationAuthorId)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.ResponsibleUserId)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.TimeZone)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.ScheduledDate)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.OccurredAt)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.Status)}
            FROM {EntityMapper.TbName<RiskReviewDal>()} rr JOIN {EntityMapper.TbName<ReviewRunDal>()} run ON run.{EntityMapper.ColName<ReviewRunDal>(x => x.Id)} = rr.{EntityMapper.ColName<RiskReviewDal>(x => x.ReviewRunId)}
            WHERE rr.{EntityMapper.ColName<RiskReviewDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(id))}
            ORDER BY (rr.{EntityMapper.ColName<RiskReviewDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(waiting))}) DESC, rr.{EntityMapper.ColName<RiskReviewDal>(x => x.ProcessedAt)} DESC, rr.{EntityMapper.ColName<RiskReviewDal>(x => x.Id)} DESC
            OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var rows = await _connection.QueryAsync<RiskReviewDal, ReviewRunDal, RiskReviewView>(query,
            (review, run) => new RiskReviewView(review, run), new { id, offset, limit, waiting }, transaction,
            splitOn: EntityMapper.ColName<ReviewRunDal>(x => x.Id, false));
        return rows.ToArray();
    }

    /// <inheritdoc/>
    public async Task InsertReviewRunAsync(ReviewRunDal row, IDbTransaction transaction)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<ReviewRunDal>()}
                ({EntityMapper.ColName<ReviewRunDal>(x => x.Id)}, {EntityMapper.ColName<ReviewRunDal>(x => x.Reason)}, {EntityMapper.ColName<ReviewRunDal>(x => x.SourceType)}, {EntityMapper.ColName<ReviewRunDal>(x => x.SourceId)}, {EntityMapper.ColName<ReviewRunDal>(x => x.SourceNameSnapshot)}, {EntityMapper.ColName<ReviewRunDal>(x => x.ExternalSystemNameSnapshot)}, {EntityMapper.ColName<ReviewRunDal>(x => x.InitiatedById)}, {EntityMapper.ColName<ReviewRunDal>(x => x.ConfigurationAuthorId)}, {EntityMapper.ColName<ReviewRunDal>(x => x.ResponsibleUserId)}, {EntityMapper.ColName<ReviewRunDal>(x => x.TimeZone)}, {EntityMapper.ColName<ReviewRunDal>(x => x.ScheduledDate)}, {EntityMapper.ColName<ReviewRunDal>(x => x.OccurredAt)}, {EntityMapper.ColName<ReviewRunDal>(x => x.Status)}, {EntityMapper.ColName<ReviewRunDal>(x => x.RawPayload)}, {EntityMapper.ColName<ReviewRunDal>(x => x.TargetRiskIds)})
            VALUES ({EntityMapper.ParameterName(nameof(row.Id))}, {EntityMapper.ParameterName(nameof(row.Reason))}, {EntityMapper.ParameterName(nameof(row.SourceType))}, {EntityMapper.ParameterName(nameof(row.SourceId))}, {EntityMapper.ParameterName(nameof(row.SourceNameSnapshot))}, {EntityMapper.ParameterName(nameof(row.ExternalSystemNameSnapshot))}, {EntityMapper.ParameterName(nameof(row.InitiatedById))}, {EntityMapper.ParameterName(nameof(row.ConfigurationAuthorId))}, {EntityMapper.ParameterName(nameof(row.ResponsibleUserId))}, {EntityMapper.ParameterName(nameof(row.TimeZone))}, {EntityMapper.ParameterName(nameof(row.ScheduledDate))}, {EntityMapper.ParameterName(nameof(row.OccurredAt))}, {EntityMapper.ParameterName(nameof(row.Status))}, {EntityMapper.ParameterName(nameof(row.RawPayload))}, {EntityMapper.ParameterName(nameof(row.TargetRiskIds))})
            """;
        await _connection.ExecuteAsync(query, row, transaction);
    }

    /// <inheritdoc/>
    public async Task<RiskDal> OpenReviewAsync(RiskDal risk, ReviewRunDal run, Guid operationId, IDbTransaction transaction,
        List<(string Path, object? Before, object After)>? relatedChanges = null)
    {
        if (await ReadWaitingReviewAsync(risk.Id, transaction) is not null)
        {
            throw new IdentityCommandException(409, RiskRules.InvalidTransition);
        }

        var current = await _riskAssessmentStateRepository.ReadCurrentAssessmentAsync(risk.Id, transaction);
        var previous = current ?? await ReadLatestAssessmentAsync(risk.Id, transaction);
        var changes = relatedChanges ?? [];
        if (current is not null)
        {
            var mode = run.SourceType == ReviewRules.Transfer ? ReviewRules.TransferMode : current.ResponseMode;
            var historical = await _riskAssessmentStateRepository.SetAssessmentStateAsync(transaction, current, false, mode);
            changes.Add((BusinessHistory.AssessmentsPath, current, historical));
        }

        var updated = await _riskAssessmentStateRepository.SaveRiskStateAsync(transaction, risk, RiskRules.AwaitingReview, null, clearCloseReason: true);
        await InsertRiskReviewAsync(run.Id, risk.Id, ReviewRules.Waiting, transaction, previous?.Id, run.InitiatedById);
        var integration = run.SourceType == ReviewRules.ReviewWebhook || run.ExternalSystemNameSnapshot == JiraRules.Source;
        var automatic = integration || run.SourceType == ReviewRules.ReviewSchedule || run.Reason == ReviewRules.AfterTreatment;
        var responsible = automatic ? run.ResponsibleUserId ?? run.InitiatedById : run.InitiatedById;
        await BusinessHistory.RecordAsync(transaction, operationId, responsible,
            risk.Id, risk, updated, system: automatic, relatedChanges: changes, integration: integration,
            automaticSource: integration ? run.ExternalSystemNameSnapshot ?? JiraRules.Source : null,
            configurationAuthorId: automatic ? run.ConfigurationAuthorId : null);
        return updated;
    }

    /// <inheritdoc/>
    public async Task<RiskDal> StartSingleReviewAsync(RiskDal risk, string reason, string source, Guid sourceId, Guid actor,
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

    /// <inheritdoc/>
    public async Task InsertRiskReviewAsync(Guid runId, Guid riskId, string status, IDbTransaction transaction,
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
        await _connection.ExecuteAsync(query, row, transaction);
    }

    /// <inheritdoc/>
    public Task<RiskReviewDal?> ReadWaitingReviewAsync(Guid riskId, IDbTransaction transaction)
    {
        var waiting = ReviewRules.Waiting;
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<RiskReviewDal>()} WHERE {EntityMapper.ColName<RiskReviewDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(riskId))} AND {EntityMapper.ColName<RiskReviewDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(waiting))}
            """;
        return _connection.QuerySingleOrDefaultAsync<RiskReviewDal>(query, new { riskId, waiting }, transaction);
    }

    /// <inheritdoc/>
    public Task<RiskAssessmentDal?> ReadLatestAssessmentAsync(Guid id, IDbTransaction transaction)
    {
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<RiskAssessmentDal>()} WHERE {EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(id))}
            ORDER BY {EntityMapper.ColName<RiskAssessmentDal>(x => x.CreatedAt)} DESC, {EntityMapper.ColName<RiskAssessmentDal>(x => x.Id)} DESC LIMIT 1
            """;
        return _connection.QuerySingleOrDefaultAsync<RiskAssessmentDal>(query, new { id }, transaction);
    }

    /// <inheritdoc/>
    public async Task CompleteReviewAsync(Guid riskId, Guid actor, IDbTransaction transaction, Guid? assessmentId, string? closeReason)
    {
        var completed = ReviewRules.Completed;
        var waiting = ReviewRules.Waiting;
        var query = $"""
            UPDATE {EntityMapper.TbName<RiskReviewDal>()} SET {EntityMapper.ColName<RiskReviewDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(completed))}, {EntityMapper.ColName<RiskReviewDal>(x => x.CompletedAt)} = clock_timestamp(),
                {EntityMapper.ColName<RiskReviewDal>(x => x.CompletedById)} = {EntityMapper.ParameterName(nameof(actor))}, {EntityMapper.ColName<RiskReviewDal>(x => x.NewAssessmentId)} = {EntityMapper.ParameterName(nameof(assessmentId))}, {EntityMapper.ColName<RiskReviewDal>(x => x.CloseReason)} = {EntityMapper.ParameterName(nameof(closeReason))}
            WHERE {EntityMapper.ColName<RiskReviewDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(riskId))} AND {EntityMapper.ColName<RiskReviewDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(waiting))}
            """;
        if (await _connection.ExecuteAsync(query, new { riskId, actor, assessmentId, closeReason, completed, waiting }, transaction) != 1)
        {
            throw new IdentityCommandException(409, RiskRules.InvalidTransition);
        }
    }

    /// <inheritdoc/>
    public async Task RequireReviewResponsibleAsync(Guid userId, IDbTransaction transaction)
    {
        var query = $"""
            SELECT EXISTS (SELECT FROM {EntityMapper.TbName<UserProfileDal>()} WHERE {EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = {EntityMapper.ParameterName(nameof(userId))} AND {EntityMapper.ColName<UserProfileDal>(x => x.IsActive)})
            """;
        if (!await _connection.ExecuteScalarAsync<bool>(query, new { userId }, transaction))
        {
            throw new IdentityCommandException(400, ReviewRules.Invalid);
        }
    }
}
