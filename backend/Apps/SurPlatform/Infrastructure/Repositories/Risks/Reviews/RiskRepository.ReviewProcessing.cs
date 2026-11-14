using System.Data;
using System.Security.Cryptography;
using Infrastructure.Repositories.Identity;
using Npgsql;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Repositories.Risks.Helpers;

namespace Infrastructure.Repositories.Risks;

/// <summary>Приём вебхуков и возобновляемая обработка автоматических запусков</summary>
internal sealed partial class RiskRepository
{
    /// <summary>Число запусков клиента за один проход</summary>
    private const int ReviewRunBatch = 20;

    /// <inheritdoc/>
    public async Task AuthorizeReviewWebhookAsync(Guid id, string token)
    {
        using var transaction = connection.BeginTransaction();
        await ReadAuthorizedWebhookAsync(id, token, transaction);
        transaction.Commit();
    }

    /// <inheritdoc/>
    public async Task<ReviewWebhookAccepted> AcceptReviewWebhookAsync(Guid id, string token, byte[] payload)
    {
        using var transaction = connection.BeginTransaction();
        await AdministrationTransaction.LockAsync(connection, transaction);
        var webhook = await ReadAuthorizedWebhookAsync(id, token, transaction);
        var run = new ReviewRunDal
        {
            Id = Guid.NewGuid(), Reason = ReviewRules.Webhook, SourceType = ReviewRules.ReviewWebhook, SourceId = id,
            SourceNameSnapshot = webhook.Name, ExternalSystemNameSnapshot = webhook.ExternalSystemName,
            InitiatedById = IdentityAccounts.SystemUserId, ConfigurationAuthorId = webhook.UpdatedById,
            ResponsibleUserId = webhook.ResponsibleUserId, OccurredAt = DateTime.UtcNow,
            Status = ReviewRules.Processing, RawPayload = payload, TargetRiskIds = await ReadWebhookTargetsAsync(id, transaction)
        };
        await InsertReviewRunAsync(run, transaction);
        transaction.Commit();
        return new ReviewWebhookAccepted(run.Id);
    }

    /// <summary>Проверяет отдельный секрет без раскрытия наличия настройки</summary>
    /// <param name="id">Ключ адреса</param>
    /// <param name="token">Bearer-секрет</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <returns>Действующая настройка</returns>
    private async Task<ReviewWebhookDal> ReadAuthorizedWebhookAsync(Guid id, string token, IDbTransaction transaction)
    {
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<ReviewWebhookDal>()} WHERE {EntityMapper.ColName<ReviewWebhookDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))} AND {EntityMapper.ColName<ReviewWebhookDal>(x => x.IsActive)}
            """;
        var webhook = await connection.QuerySingleOrDefaultAsync<ReviewWebhookDal>(query, new { id }, transaction);
        if (webhook is null || token.Length != ReviewRules.TokenLength
            || !CryptographicOperations.FixedTimeEquals(webhook.TokenHash, HashReviewToken(token)))
        {
            throw new IdentityCommandException(401, IdentityErrorCodes.AccessDenied);
        }
        return webhook;
    }

    /// <inheritdoc/>
    public async Task ProcessReviewsAsync(CancellationToken cancellationToken)
    {
        var scheduleQuery = $"""
            SELECT {EntityMapper.ColName<ReviewScheduleDal>(x => x.Id)} FROM {EntityMapper.TbName<ReviewScheduleDal>()} WHERE {EntityMapper.ColName<ReviewScheduleDal>(x => x.IsActive)} ORDER BY {EntityMapper.ColName<ReviewScheduleDal>(x => x.Id)}
            """;
        var schedules = await connection.QueryAsync<Guid>(new CommandDefinition(scheduleQuery, cancellationToken: cancellationToken));
        foreach (var scheduleId in schedules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RegisterScheduledReviewAsync(scheduleId);
        }

        var processing = ReviewRules.Processing;
        var limit = ReviewRunBatch;
        var query = $"""
            SELECT {EntityMapper.ColName<ReviewRunDal>(x => x.Id)}, {EntityMapper.ColName<ReviewRunDal>(x => x.Reason)},
                {EntityMapper.ColName<ReviewRunDal>(x => x.SourceType)}, {EntityMapper.ColName<ReviewRunDal>(x => x.InitiatedById)},
                {EntityMapper.ColName<ReviewRunDal>(x => x.ConfigurationAuthorId)}, {EntityMapper.ColName<ReviewRunDal>(x => x.ResponsibleUserId)},
                {EntityMapper.ColName<ReviewRunDal>(x => x.TargetRiskIds)}
            FROM {EntityMapper.TbName<ReviewRunDal>()} WHERE {EntityMapper.ColName<ReviewRunDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(processing))}
            ORDER BY {EntityMapper.ColName<ReviewRunDal>(x => x.OccurredAt)}, {EntityMapper.ColName<ReviewRunDal>(x => x.Id)} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var runs = await connection.QueryAsync<ReviewRunDal>(new CommandDefinition(query, new { processing, limit },
            cancellationToken: cancellationToken));
        foreach (var run in runs)
        {
            foreach (var riskId in run.TargetRiskIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await ProcessReviewTargetAsync(run, riskId, false);
                }
                catch (Exception error) when (!cancellationToken.IsCancellationRequested &&
                    error is not (OperationCanceledException or NpgsqlException { IsTransient: true }))
                {
                    await ProcessReviewTargetAsync(run, riskId, true);
                }
            }
            await FinishReviewRunAsync(run);
        }
    }

    /// <summary>Регистрирует сегодняшнюю дату и текущую выборку под общей блокировкой</summary>
    /// <param name="id">Расписание</param>
    private async Task RegisterScheduledReviewAsync(Guid id)
    {
        using var transaction = connection.BeginTransaction();
        await AdministrationTransaction.LockAsync(connection, transaction);
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<ReviewScheduleDal>()} WHERE {EntityMapper.ColName<ReviewScheduleDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))} AND {EntityMapper.ColName<ReviewScheduleDal>(x => x.IsActive)}
            """;
        var schedule = await connection.QuerySingleOrDefaultAsync<ReviewScheduleDal>(query, new { id }, transaction);
        var now = DateTime.UtcNow;
        if (schedule is not null)
        {
            var localDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZone)));
            if (ReviewCalendar.IsDue(schedule, localDay) && await CanRegisterScheduleAsync(schedule, localDay, transaction))
            {
                var run = new ReviewRunDal
                {
                    Id = Guid.NewGuid(), Reason = ReviewRules.Schedule, SourceType = ReviewRules.ReviewSchedule, SourceId = id,
                    SourceNameSnapshot = schedule.Name, InitiatedById = IdentityAccounts.SystemUserId,
                    ConfigurationAuthorId = schedule.UpdatedById, ResponsibleUserId = schedule.ResponsibleUserId,
                    TimeZone = schedule.TimeZone, ScheduledDate = localDay.ToDateTime(TimeOnly.MinValue), OccurredAt = now,
                    Status = ReviewRules.Processing, TargetRiskIds = await ReadAcceptedRiskIdsAsync(transaction)
                };
                await InsertReviewRunAsync(run, transaction);
            }
        }
        transaction.Commit();
    }

    /// <summary>Проверяет ключ календарной даты и лимит включая пустые запуски</summary>
    /// <param name="schedule">Расписание</param>
    /// <param name="day">Плановая дата</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <returns>Можно ли зарегистрировать срабатывание</returns>
    private async Task<bool> CanRegisterScheduleAsync(ReviewScheduleDal schedule, DateOnly day, IDbTransaction transaction)
    {
        var sourceId = schedule.Id;
        var scheduledDate = day.ToDateTime(TimeOnly.MinValue);
        var occurrenceCount = schedule.OccurrenceCount;
        var query = $"""
            SELECT COUNT(*) FILTER (WHERE {EntityMapper.ColName<ReviewRunDal>(x => x.ScheduledDate)} = {EntityMapper.ParameterName(nameof(scheduledDate))}) = 0
                AND (CAST({EntityMapper.ParameterName(nameof(occurrenceCount))} AS integer) IS NULL OR COUNT(*) < {EntityMapper.ParameterName(nameof(occurrenceCount))})
            FROM {EntityMapper.TbName<ReviewRunDal>()} WHERE {EntityMapper.ColName<ReviewRunDal>(x => x.SourceId)} = {EntityMapper.ParameterName(nameof(sourceId))} AND {EntityMapper.ColName<ReviewRunDal>(x => x.ScheduledDate)} IS NOT NULL
            """;
        return await connection.ExecuteScalarAsync<bool>(query, new { sourceId, scheduledDate, occurrenceCount }, transaction);
    }

    /// <summary>Читает текущие принятые риски или проверяет новую область вебхука</summary>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <param name="ids">При наличии только проверяемые идентификаторы</param>
    /// <returns>Действующие кандидаты</returns>
    private async Task<Guid[]> ReadAcceptedRiskIdsAsync(IDbTransaction transaction, Guid[]? ids = null)
    {
        var accepted = RiskRules.Accepted;
        var query = $"""
            SELECT {EntityMapper.ColName<RiskDal>(x => x.Id)} FROM {EntityMapper.TbName<RiskDal>()} WHERE {EntityMapper.ColName<RiskDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(accepted))}
                AND (CAST({EntityMapper.ParameterName(nameof(ids))} AS uuid[]) IS NULL OR {EntityMapper.ColName<RiskDal>(x => x.Id)} = ANY({EntityMapper.ParameterName(nameof(ids))}))
            ORDER BY {EntityMapper.ColName<RiskDal>(x => x.Id)}
            """;
        return (await connection.QueryAsync<Guid>(query, new { ids, accepted }, transaction)).ToArray();
    }

    /// <summary>Обрабатывает цель идемпотентно в отдельной транзакции</summary>
    /// <param name="run">Сохранённый запуск</param>
    /// <param name="riskId">Риск из снимка</param>
    /// <param name="failed">Фиксируется ошибка откатившейся попытки</param>
    private async Task ProcessReviewTargetAsync(ReviewRunDal run, Guid riskId, bool failed)
    {
        using var transaction = connection.BeginTransaction();
        await AdministrationTransaction.LockAsync(connection, transaction);
        var runId = run.Id;
        var query = $"""
            SELECT EXISTS (SELECT FROM {EntityMapper.TbName<RiskReviewDal>()} WHERE {EntityMapper.ColName<RiskReviewDal>(x => x.ReviewRunId)} = {EntityMapper.ParameterName(nameof(runId))}
                AND {EntityMapper.ColName<RiskReviewDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(riskId))})
            """;
        if (!await connection.ExecuteScalarAsync<bool>(query, new { runId, riskId }, transaction))
        {
            if (failed)
            {
                await InsertRiskReviewAsync(runId, riskId, ReviewRules.Error, transaction);
            }
            else
            {
                var risk = await ReadAsync<RiskDal>(riskId, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
                if (risk.Status == RiskRules.Accepted && await ReadWaitingReviewAsync(riskId, transaction) is null)
                {
                    await OpenReviewAsync(risk, run, Guid.NewGuid(), transaction);
                }
                else
                {
                    await InsertRiskReviewAsync(runId, riskId, ReviewRules.NoAction, transaction);
                }
            }
        }
        transaction.Commit();
    }

    /// <summary>Фиксирует итог только после обработки всех сохранённых целей</summary>
    /// <param name="run">Сохранённый запуск</param>
    private async Task FinishReviewRunAsync(ReviewRunDal run)
    {
        using var transaction = connection.BeginTransaction();
        await AdministrationTransaction.LockAsync(connection, transaction);
        var id = run.Id;
        var statusesQuery = $"""
            SELECT {EntityMapper.ColName<RiskReviewDal>(x => x.Status)} FROM {EntityMapper.TbName<RiskReviewDal>()} WHERE {EntityMapper.ColName<RiskReviewDal>(x => x.ReviewRunId)} = {EntityMapper.ParameterName(nameof(id))}
            """;
        var statuses = (await connection.QueryAsync<string>(statusesQuery, new { id }, transaction)).ToArray();
        if (statuses.Length == run.TargetRiskIds.Length)
        {
            var errors = statuses.Count(value => value == ReviewRules.Error);
            var status = errors == 0 ? ReviewRules.Completed : errors == statuses.Length ? ReviewRules.Error : ReviewRules.Partial;
            var processing = ReviewRules.Processing;
            var updateQuery = $"""
                UPDATE {EntityMapper.TbName<ReviewRunDal>()} SET {EntityMapper.ColName<ReviewRunDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(status))}
                WHERE {EntityMapper.ColName<ReviewRunDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))} AND {EntityMapper.ColName<ReviewRunDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(processing))}
                """;
            await connection.ExecuteAsync(updateQuery, new { id, status, processing }, transaction);
        }
        transaction.Commit();
    }

    /// <inheritdoc/>
    public async Task<ReviewRunDal[]> ListReviewRunsAsync(RiskActor actor, int offset, int limit)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, IdentityRights.AdminCode, transaction);
        var query = $"""
            SELECT {EntityMapper.ColName<ReviewRunDal>(x => x.Id)}, {EntityMapper.ColName<ReviewRunDal>(x => x.Reason)}, {EntityMapper.ColName<ReviewRunDal>(x => x.SourceType)}, {EntityMapper.ColName<ReviewRunDal>(x => x.SourceId)}, {EntityMapper.ColName<ReviewRunDal>(x => x.SourceNameSnapshot)}, {EntityMapper.ColName<ReviewRunDal>(x => x.ExternalSystemNameSnapshot)}, {EntityMapper.ColName<ReviewRunDal>(x => x.InitiatedById)}, {EntityMapper.ColName<ReviewRunDal>(x => x.ConfigurationAuthorId)}, {EntityMapper.ColName<ReviewRunDal>(x => x.ResponsibleUserId)}, {EntityMapper.ColName<ReviewRunDal>(x => x.TimeZone)}, {EntityMapper.ColName<ReviewRunDal>(x => x.ScheduledDate)}, {EntityMapper.ColName<ReviewRunDal>(x => x.OccurredAt)}, {EntityMapper.ColName<ReviewRunDal>(x => x.Status)}
            FROM {EntityMapper.TbName<ReviewRunDal>()} ORDER BY {EntityMapper.ColName<ReviewRunDal>(x => x.OccurredAt)} DESC, {EntityMapper.ColName<ReviewRunDal>(x => x.Id)} DESC
            OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var rows = (await connection.QueryAsync<ReviewRunDal>(query, new { offset, limit }, transaction)).ToArray();
        transaction.Commit();
        return rows;
    }
}
