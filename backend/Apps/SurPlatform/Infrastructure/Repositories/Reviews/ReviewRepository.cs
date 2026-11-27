using Infrastructure.Repositories.Reviews.Helpers;
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
using Infrastructure.Repositories.Identity;
using Infrastructure.Repositories.Reviews.Interfaces;
using Infrastructure.Repositories.Risks.Interfaces;
using Infrastructure.Repositories.Sessions.Interfaces;
using Infrastructure.Repositories.Shared;
using Infrastructure.Repositories.Treatments.Interfaces;
using Npgsql;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Domain.DalModels.Reviews;
using Domain.DalModels.Risks;

namespace Infrastructure.Repositories.Reviews;

/// <summary>Настройка и обработка пересмотров</summary>
internal sealed class ReviewRepository : ClientRepository, IReviewRepository, ITransactionalReviewWorkerRepository
{
    /// <summary>Запуск и завершение пересмотра в общей транзакции</summary>
    private readonly IReviewStateRepository _reviewStateRepository;

    /// <summary>Текущая оценка и версии состояния риска</summary>
    private readonly IRiskAssessmentStateRepository _riskAssessmentStateRepository;

    /// <summary>Паспорт, связи процессов и технический пересчет риска</summary>
    private readonly IRiskStateRepository _riskStateRepository;

    /// <summary>Назначения и переходы состояния мер</summary>
    private readonly ITreatmentStateRepository _treatmentStateRepository;

    /// <summary>Инициализирует операции выбранной клиентской базы</summary>
    /// <param name="connection">Подключение выбранной клиентской базы</param>
    /// <param name="sessions">Проверка сессии и эффективных прав в транзакции</param>
    /// <param name="operations">Метрики операций базы</param>
    /// <param name="reviewStateRepository">Запуск и завершение пересмотра в общей транзакции</param>
    /// <param name="riskAssessmentStateRepository">Текущая оценка и версии состояния риска</param>
    /// <param name="riskStateRepository">Паспорт, связи процессов и технический пересчет риска</param>
    /// <param name="treatmentStateRepository">Назначения и переходы состояния мер</param>
    public ReviewRepository(
        IDbConnection connection,
        ITransactionalSessionRepository sessions,
        OperationsMetrics operations,
        IReviewStateRepository reviewStateRepository,
        IRiskAssessmentStateRepository riskAssessmentStateRepository,
        IRiskStateRepository riskStateRepository,
        ITreatmentStateRepository treatmentStateRepository)
        : base(connection, sessions, operations)
    {
        _reviewStateRepository = reviewStateRepository;
        _riskAssessmentStateRepository = riskAssessmentStateRepository;
        _riskStateRepository = riskStateRepository;
        _treatmentStateRepository = treatmentStateRepository;
    }

    /// <inheritdoc/>
    public Task<RiskDal> TransferRiskAsync(RiskActor actor, Guid id, TransferRiskRequest request)
    {
        return WriteAsync(actor, ReviewRules.TransferRight, async transaction =>
        {
            await _riskStateRepository.RequirePrimaryOwnerAsync(actor, id, transaction);
            var risk = await ReadAsync<RiskDal>(id, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
            RequireVersion(risk.Version, request.Version);
            var current = await _riskAssessmentStateRepository.ReadCurrentAssessmentAsync(id, transaction);
            if (risk.Status != RiskRules.Active || current is not { ResponseMode: null })
            {
                throw new IdentityCommandException(409, RiskRules.InvalidTransition);
            }

            var links = await _riskStateRepository.ReadActiveRiskLinksAsync(id, transaction);
            if (request.ProcessId == Guid.Empty || links.Any(link => link.ProcessId == request.ProcessId))
            {
                throw new IdentityCommandException(400, RiskRules.Invalid);
            }
            await _treatmentStateRepository.RequireNoBlockingTreatmentsAsync(transaction, id, includeDrafts: true);
            var processes = await _riskStateRepository.ReadActiveProcessesAsync([request.ProcessId], transaction);
            var changes = new List<(string Path, object? Before, object After)>();
            foreach (var link in links)
            {
                var closed = await _riskStateRepository.SaveProcessLinkAsync(transaction, link, false, false);
                changes.Add((BusinessHistory.ProcessesPath, link, closed));
            }

            var added = await _riskStateRepository.InsertProcessLinkAsync(transaction, id, processes[0], true);
            changes.Add((BusinessHistory.ProcessesPath, null, added));
            return await _reviewStateRepository.StartSingleReviewAsync(risk, ReviewRules.Manual, ReviewRules.Transfer, id,
                actor.UserId, Guid.NewGuid(), transaction, changes);
        });
    }

    /// <inheritdoc/>
    public async Task<RiskReviewView[]> ListRiskReviewsAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        await ReadAsync<RiskDal>(id, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
        var history = await _reviewStateRepository.ReadRiskReviewsAsync(id, offset, limit, transaction);
        transaction.Commit();
        return history;
    }

    /// <summary>Число запусков клиента за один проход</summary>
    private const int ReviewRunBatch = 20;

    /// <inheritdoc/>
    public async Task AuthorizeReviewWebhookAsync(Guid id, string token)
    {
        using var transaction = _connection.BeginTransaction();
        await ReadAuthorizedWebhookAsync(id, token, transaction);
        transaction.Commit();
    }

    /// <inheritdoc/>
    public async Task<ReviewWebhookAccepted> AcceptReviewWebhookAsync(Guid id, string token, byte[] payload)
    {
        using var transaction = _connection.BeginTransaction();
        await AdministrationTransaction.LockAsync(_connection, transaction);
        var webhook = await ReadAuthorizedWebhookAsync(id, token, transaction);
        var run = new ReviewRunDal
        {
            Id = Guid.NewGuid(), Reason = ReviewRules.Webhook, SourceType = ReviewRules.ReviewWebhook, SourceId = id,
            SourceNameSnapshot = webhook.Name, ExternalSystemNameSnapshot = webhook.ExternalSystemName,
            InitiatedById = IdentityAccounts.SystemUserId, ConfigurationAuthorId = webhook.UpdatedById,
            ResponsibleUserId = webhook.ResponsibleUserId, OccurredAt = DateTime.UtcNow,
            Status = ReviewRules.Processing, RawPayload = payload, TargetRiskIds = await ReadWebhookTargetsAsync(id, transaction)
        };
        await _reviewStateRepository.InsertReviewRunAsync(run, transaction);
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
        var webhook = await _connection.QuerySingleOrDefaultAsync<ReviewWebhookDal>(query, new { id }, transaction);
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
        var schedules = await _connection.QueryAsync<Guid>(new CommandDefinition(scheduleQuery, cancellationToken: cancellationToken));
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
        var runs = await _connection.QueryAsync<ReviewRunDal>(new CommandDefinition(query, new { processing, limit },
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
        using var transaction = _connection.BeginTransaction();
        await AdministrationTransaction.LockAsync(_connection, transaction);
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<ReviewScheduleDal>()} WHERE {EntityMapper.ColName<ReviewScheduleDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))} AND {EntityMapper.ColName<ReviewScheduleDal>(x => x.IsActive)}
            """;
        var schedule = await _connection.QuerySingleOrDefaultAsync<ReviewScheduleDal>(query, new { id }, transaction);
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
                await _reviewStateRepository.InsertReviewRunAsync(run, transaction);
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
        return await _connection.ExecuteScalarAsync<bool>(query, new { sourceId, scheduledDate, occurrenceCount }, transaction);
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
        return (await _connection.QueryAsync<Guid>(query, new { ids, accepted }, transaction)).ToArray();
    }

    /// <summary>Обрабатывает цель идемпотентно в отдельной транзакции</summary>
    /// <param name="run">Сохранённый запуск</param>
    /// <param name="riskId">Риск из снимка</param>
    /// <param name="failed">Фиксируется ошибка откатившейся попытки</param>
    private async Task ProcessReviewTargetAsync(ReviewRunDal run, Guid riskId, bool failed)
    {
        using var transaction = _connection.BeginTransaction();
        await AdministrationTransaction.LockAsync(_connection, transaction);
        var runId = run.Id;
        var query = $"""
            SELECT EXISTS (SELECT FROM {EntityMapper.TbName<RiskReviewDal>()} WHERE {EntityMapper.ColName<RiskReviewDal>(x => x.ReviewRunId)} = {EntityMapper.ParameterName(nameof(runId))}
                AND {EntityMapper.ColName<RiskReviewDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(riskId))})
            """;
        if (!await _connection.ExecuteScalarAsync<bool>(query, new { runId, riskId }, transaction))
        {
            if (failed)
            {
                await _reviewStateRepository.InsertRiskReviewAsync(runId, riskId, ReviewRules.Error, transaction);
            }
            else
            {
                var risk = await ReadAsync<RiskDal>(riskId, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
                if (risk.Status == RiskRules.Accepted && await _reviewStateRepository.ReadWaitingReviewAsync(riskId, transaction) is null)
                {
                    await _reviewStateRepository.OpenReviewAsync(risk, run, Guid.NewGuid(), transaction);
                }
                else
                {
                    await _reviewStateRepository.InsertRiskReviewAsync(runId, riskId, ReviewRules.NoAction, transaction);
                }
            }
        }
        transaction.Commit();
    }

    /// <summary>Фиксирует итог только после обработки всех сохранённых целей</summary>
    /// <param name="run">Сохранённый запуск</param>
    private async Task FinishReviewRunAsync(ReviewRunDal run)
    {
        using var transaction = _connection.BeginTransaction();
        await AdministrationTransaction.LockAsync(_connection, transaction);
        var id = run.Id;
        var statusesQuery = $"""
            SELECT {EntityMapper.ColName<RiskReviewDal>(x => x.Status)} FROM {EntityMapper.TbName<RiskReviewDal>()} WHERE {EntityMapper.ColName<RiskReviewDal>(x => x.ReviewRunId)} = {EntityMapper.ParameterName(nameof(id))}
            """;
        var statuses = (await _connection.QueryAsync<string>(statusesQuery, new { id }, transaction)).ToArray();
        if (statuses.Length == run.TargetRiskIds.Length)
        {
            var errors = statuses.Count(value => value == ReviewRules.Error);
            var status = errors == 0 ? ReviewRules.Completed : errors == statuses.Length ? ReviewRules.Error : ReviewRules.Partial;
            var processing = ReviewRules.Processing;
            var updateQuery = $"""
                UPDATE {EntityMapper.TbName<ReviewRunDal>()} SET {EntityMapper.ColName<ReviewRunDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(status))}
                WHERE {EntityMapper.ColName<ReviewRunDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))} AND {EntityMapper.ColName<ReviewRunDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(processing))}
                """;
            await _connection.ExecuteAsync(updateQuery, new { id, status, processing }, transaction);
        }
        transaction.Commit();
    }

    /// <inheritdoc/>
    public async Task<ReviewRunDal[]> ListReviewRunsAsync(RiskActor actor, int offset, int limit)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, IdentityRights.AdminCode, transaction);
        var query = $"""
            SELECT {EntityMapper.ColName<ReviewRunDal>(x => x.Id)}, {EntityMapper.ColName<ReviewRunDal>(x => x.Reason)}, {EntityMapper.ColName<ReviewRunDal>(x => x.SourceType)}, {EntityMapper.ColName<ReviewRunDal>(x => x.SourceId)}, {EntityMapper.ColName<ReviewRunDal>(x => x.SourceNameSnapshot)}, {EntityMapper.ColName<ReviewRunDal>(x => x.ExternalSystemNameSnapshot)}, {EntityMapper.ColName<ReviewRunDal>(x => x.InitiatedById)}, {EntityMapper.ColName<ReviewRunDal>(x => x.ConfigurationAuthorId)}, {EntityMapper.ColName<ReviewRunDal>(x => x.ResponsibleUserId)}, {EntityMapper.ColName<ReviewRunDal>(x => x.TimeZone)}, {EntityMapper.ColName<ReviewRunDal>(x => x.ScheduledDate)}, {EntityMapper.ColName<ReviewRunDal>(x => x.OccurredAt)}, {EntityMapper.ColName<ReviewRunDal>(x => x.Status)}
            FROM {EntityMapper.TbName<ReviewRunDal>()} ORDER BY {EntityMapper.ColName<ReviewRunDal>(x => x.OccurredAt)} DESC, {EntityMapper.ColName<ReviewRunDal>(x => x.Id)} DESC
            OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var rows = (await _connection.QueryAsync<ReviewRunDal>(query, new { offset, limit }, transaction)).ToArray();
        transaction.Commit();
        return rows;
    }

    /// <summary>Длина случайного секрета в байтах</summary>
    private const int ReviewSecretBytes = ReviewRules.TokenLength / 2;

    /// <inheritdoc/>
    public Task<ReviewScheduleDal[]> ListReviewSchedulesAsync(RiskActor actor, int offset, int limit)
    {
        return ListReviewSettingsAsync<ReviewScheduleDal>(actor, offset, limit, EntityMapper.ColName<ReviewScheduleDal>(x => x.Id));
    }

    /// <inheritdoc/>
    public Task<ReviewWebhookDal[]> ListReviewWebhooksAsync(RiskActor actor, int offset, int limit)
    {
        return ListReviewSettingsAsync<ReviewWebhookDal>(actor, offset, limit, EntityMapper.ColName<ReviewWebhookDal>(x => x.Id));
    }

    /// <inheritdoc/>
    public Task<ReviewScheduleDal> SaveReviewScheduleAsync(RiskActor actor, Guid? id, ReviewScheduleRequest request, string timeZone)
    {
        return WriteAsync(actor, IdentityRights.AdminCode, async transaction =>
        {
            ReviewCalendar.Validate(request);
            var previous = id is Guid key
                ? await ReadAsync<ReviewScheduleDal>(key, EntityMapper.ColName<ReviewScheduleDal>(x => x.Id), transaction) : null;
            if (previous is not null)
            {
                RequireVersion(previous.Version, request.Version);
            }
            await _reviewStateRepository.RequireReviewResponsibleAsync(request.ResponsibleUserId, transaction);
            var zone = previous?.TimeZone ?? timeZone;
            try
            {
                _ = TimeZoneInfo.FindSystemTimeZoneById(zone);
            }
            catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                throw new IdentityCommandException(400, ReviewRules.Invalid);
            }

            var now = DateTime.UtcNow;
            var row = new ReviewScheduleDal
            {
                Id = previous?.Id ?? Guid.NewGuid(), Version = Guid.NewGuid(), Name = RequiredText(request.Name),
                IsActive = request.IsActive, ResponsibleUserId = request.ResponsibleUserId,
                TimeZone = zone, StartDate = request.StartDate.ToDateTime(TimeOnly.MinValue),
                RecurrenceType = request.RecurrenceType, RecurrenceInterval = request.RecurrenceInterval,
                Weekdays = request.Weekdays ?? [], EndType = request.EndType, OccurrenceCount = request.OccurrenceCount,
                EndDate = request.EndDate?.ToDateTime(TimeOnly.MinValue),
                CreatedAt = previous?.CreatedAt ?? now, CreatedById = previous?.CreatedById ?? actor.UserId,
                UpdatedAt = now, UpdatedById = actor.UserId
            };
            var insertQuery = $"""
                INSERT INTO {EntityMapper.TbName<ReviewScheduleDal>()}
                    ({EntityMapper.ColName<ReviewScheduleDal>(x => x.Id)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.Version)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.Name)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.IsActive)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.ResponsibleUserId)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.CreatedById)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.UpdatedById)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.CreatedAt)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.UpdatedAt)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.TimeZone)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.StartDate)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.RecurrenceType)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.RecurrenceInterval)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.Weekdays)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.EndType)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.OccurrenceCount)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.EndDate)})
                VALUES ({EntityMapper.ParameterName(nameof(row.Id))}, {EntityMapper.ParameterName(nameof(row.Version))}, {EntityMapper.ParameterName(nameof(row.Name))}, {EntityMapper.ParameterName(nameof(row.IsActive))}, {EntityMapper.ParameterName(nameof(row.ResponsibleUserId))}, {EntityMapper.ParameterName(nameof(row.CreatedById))}, {EntityMapper.ParameterName(nameof(row.UpdatedById))}, {EntityMapper.ParameterName(nameof(row.CreatedAt))}, {EntityMapper.ParameterName(nameof(row.UpdatedAt))}, {EntityMapper.ParameterName(nameof(row.TimeZone))}, {EntityMapper.ParameterName(nameof(row.StartDate))}, {EntityMapper.ParameterName(nameof(row.RecurrenceType))}, {EntityMapper.ParameterName(nameof(row.RecurrenceInterval))}, {EntityMapper.ParameterName(nameof(row.Weekdays))}, {EntityMapper.ParameterName(nameof(row.EndType))}, {EntityMapper.ParameterName(nameof(row.OccurrenceCount))}, {EntityMapper.ParameterName(nameof(row.EndDate))}) RETURNING *
                """;
            var updateQuery = $"""
                UPDATE {EntityMapper.TbName<ReviewScheduleDal>()} SET {EntityMapper.ColName<ReviewScheduleDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(row.Version))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.UpdatedAt)} = {EntityMapper.ParameterName(nameof(row.UpdatedAt))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.UpdatedById)} = {EntityMapper.ParameterName(nameof(row.UpdatedById))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.Name)} = {EntityMapper.ParameterName(nameof(row.Name))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.IsActive)} = {EntityMapper.ParameterName(nameof(row.IsActive))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.ResponsibleUserId)} = {EntityMapper.ParameterName(nameof(row.ResponsibleUserId))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.StartDate)} = {EntityMapper.ParameterName(nameof(row.StartDate))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.RecurrenceType)} = {EntityMapper.ParameterName(nameof(row.RecurrenceType))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.RecurrenceInterval)} = {EntityMapper.ParameterName(nameof(row.RecurrenceInterval))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.Weekdays)} = {EntityMapper.ParameterName(nameof(row.Weekdays))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.EndType)} = {EntityMapper.ParameterName(nameof(row.EndType))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.OccurrenceCount)} = {EntityMapper.ParameterName(nameof(row.OccurrenceCount))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.EndDate)} = {EntityMapper.ParameterName(nameof(row.EndDate))}
                WHERE {EntityMapper.ColName<ReviewScheduleDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(row.Id))} RETURNING *
                """;
            return await _connection.QuerySingleAsync<ReviewScheduleDal>(previous is null ? insertQuery : updateQuery, row, transaction);
        });
    }

    /// <inheritdoc/>
    public Task<bool> DeleteReviewScheduleAsync(RiskActor actor, Guid id, Guid version)
    {
        return DeleteReviewSettingAsync<ReviewScheduleDal>(actor, id, version, EntityMapper.ColName<ReviewScheduleDal>(x => x.Id),
            EntityMapper.ColName<ReviewScheduleDal>(x => x.Version));
    }

    /// <inheritdoc/>
    public Task<bool> DeleteReviewWebhookAsync(RiskActor actor, Guid id, Guid version)
    {
        return DeleteReviewSettingAsync<ReviewWebhookDal>(actor, id, version, EntityMapper.ColName<ReviewWebhookDal>(x => x.Id),
            EntityMapper.ColName<ReviewWebhookDal>(x => x.Version));
    }

    /// <inheritdoc/>
    public async Task<ReviewWebhookView> GetReviewWebhookAsync(RiskActor actor, Guid id)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, IdentityRights.AdminCode, transaction);
        var row = await ReadAsync<ReviewWebhookDal>(id, EntityMapper.ColName<ReviewWebhookDal>(x => x.Id), transaction);
        var targets = await ReadWebhookTargetsAsync(id, transaction);
        transaction.Commit();
        return new ReviewWebhookView(row, targets);
    }

    /// <inheritdoc/>
    public Task<ReviewWebhookReceipt> CreateReviewWebhookAsync(RiskActor actor, ReviewWebhookRequest request)
    {
        return WriteAsync(actor, IdentityRights.AdminCode, async transaction =>
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(ReviewSecretBytes));
            var webhook = await SaveReviewWebhookAsync(actor, null, request, HashReviewToken(token), transaction);
            return new ReviewWebhookReceipt(webhook, token);
        });
    }

    /// <inheritdoc/>
    public Task<ReviewWebhookView> UpdateReviewWebhookAsync(RiskActor actor, Guid id, ReviewWebhookRequest request)
    {
        return WriteAsync(actor, IdentityRights.AdminCode, transaction => SaveReviewWebhookAsync(actor, id, request, null, transaction));
    }

    /// <inheritdoc/>
    public Task<ReviewWebhookReceipt> RotateReviewWebhookAsync(RiskActor actor, Guid id, RotateReviewTokenRequest request)
    {
        return WriteAsync(actor, IdentityRights.AdminCode, async transaction =>
        {
            var previous = await ReadAsync<ReviewWebhookDal>(id, EntityMapper.ColName<ReviewWebhookDal>(x => x.Id), transaction);
            RequireVersion(previous.Version, request.Version);
            if (!request.Confirmed)
            {
                throw new IdentityCommandException(400, ReviewRules.Invalid);
            }
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(ReviewSecretBytes));
            var row = previous with { TokenHash = HashReviewToken(token), Version = Guid.NewGuid(), UpdatedById = actor.UserId, UpdatedAt = DateTime.UtcNow };
            var query = $"""
                UPDATE {EntityMapper.TbName<ReviewWebhookDal>()} SET {EntityMapper.ColName<ReviewWebhookDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(row.Version))},
                    {EntityMapper.ColName<ReviewWebhookDal>(x => x.UpdatedAt)} = {EntityMapper.ParameterName(nameof(row.UpdatedAt))},
                    {EntityMapper.ColName<ReviewWebhookDal>(x => x.UpdatedById)} = {EntityMapper.ParameterName(nameof(row.UpdatedById))},
                    {EntityMapper.ColName<ReviewWebhookDal>(x => x.TokenHash)} = {EntityMapper.ParameterName(nameof(row.TokenHash))}
                WHERE {EntityMapper.ColName<ReviewWebhookDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(row.Id))} RETURNING *
                """;
            var updated = await _connection.QuerySingleAsync<ReviewWebhookDal>(query, row, transaction);
            return new ReviewWebhookReceipt(new ReviewWebhookView(updated, await ReadWebhookTargetsAsync(id, transaction)), token);
        });
    }

    /// <summary>Сохраняет настройки и меняет только добавленные или удалённые связи</summary>
    /// <param name="actor">Администратор</param>
    /// <param name="id">Настройка при редактировании</param>
    /// <param name="request">Поля настройки</param>
    /// <param name="hash">Хеш при регистрации нового источника</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <returns>Настройка и её область</returns>
    private async Task<ReviewWebhookView> SaveReviewWebhookAsync(RiskActor actor, Guid? id, ReviewWebhookRequest request, byte[]? hash, IDbTransaction transaction)
    {
        var previous = id is Guid key
            ? await ReadAsync<ReviewWebhookDal>(key, EntityMapper.ColName<ReviewWebhookDal>(x => x.Id), transaction) : null;
        if (previous is not null)
        {
            RequireVersion(previous.Version, request.Version);
        }
        await _reviewStateRepository.RequireReviewResponsibleAsync(request.ResponsibleUserId, transaction);
        var oldTargets = previous is null ? [] : await ReadWebhookTargetsAsync(previous.Id, transaction);
        var targets = request.RiskIds ?? [];
        if (targets.Length == 0 || targets.Contains(Guid.Empty) || targets.Distinct().Count() != targets.Length)
        {
            throw new IdentityCommandException(400, ReviewRules.Invalid);
        }

        var added = targets.Except(oldTargets).ToArray();
        if (added.Length > 0 && (await ReadAcceptedRiskIdsAsync(transaction, added)).Length != added.Length)
        {
            throw new IdentityCommandException(409, RiskRules.InvalidTransition);
        }

        var now = DateTime.UtcNow;
        var row = new ReviewWebhookDal
        {
            Id = previous?.Id ?? Guid.NewGuid(), Version = Guid.NewGuid(), Name = RequiredText(request.Name),
            ExternalSystemName = RequiredText(request.ExternalSystemName), IsActive = request.IsActive,
            ResponsibleUserId = request.ResponsibleUserId, TokenHash = previous?.TokenHash ?? hash
                ?? throw new InvalidOperationException(ReviewRules.Invalid),
            CreatedAt = previous?.CreatedAt ?? now, CreatedById = previous?.CreatedById ?? actor.UserId,
            UpdatedAt = now, UpdatedById = actor.UserId
        };
        var insertQuery = $"""
            INSERT INTO {EntityMapper.TbName<ReviewWebhookDal>()}
                ({EntityMapper.ColName<ReviewWebhookDal>(x => x.Id)}, {EntityMapper.ColName<ReviewWebhookDal>(x => x.Version)}, {EntityMapper.ColName<ReviewWebhookDal>(x => x.Name)}, {EntityMapper.ColName<ReviewWebhookDal>(x => x.IsActive)}, {EntityMapper.ColName<ReviewWebhookDal>(x => x.ResponsibleUserId)}, {EntityMapper.ColName<ReviewWebhookDal>(x => x.CreatedById)}, {EntityMapper.ColName<ReviewWebhookDal>(x => x.UpdatedById)}, {EntityMapper.ColName<ReviewWebhookDal>(x => x.CreatedAt)}, {EntityMapper.ColName<ReviewWebhookDal>(x => x.UpdatedAt)}, {EntityMapper.ColName<ReviewWebhookDal>(x => x.ExternalSystemName)}, {EntityMapper.ColName<ReviewWebhookDal>(x => x.TokenHash)})
            VALUES ({EntityMapper.ParameterName(nameof(row.Id))}, {EntityMapper.ParameterName(nameof(row.Version))}, {EntityMapper.ParameterName(nameof(row.Name))}, {EntityMapper.ParameterName(nameof(row.IsActive))}, {EntityMapper.ParameterName(nameof(row.ResponsibleUserId))}, {EntityMapper.ParameterName(nameof(row.CreatedById))}, {EntityMapper.ParameterName(nameof(row.UpdatedById))}, {EntityMapper.ParameterName(nameof(row.CreatedAt))}, {EntityMapper.ParameterName(nameof(row.UpdatedAt))}, {EntityMapper.ParameterName(nameof(row.ExternalSystemName))}, {EntityMapper.ParameterName(nameof(row.TokenHash))}) RETURNING *
            """;
        var updateQuery = $"""
            UPDATE {EntityMapper.TbName<ReviewWebhookDal>()} SET {EntityMapper.ColName<ReviewWebhookDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(row.Version))},
                {EntityMapper.ColName<ReviewWebhookDal>(x => x.UpdatedAt)} = {EntityMapper.ParameterName(nameof(row.UpdatedAt))},
                {EntityMapper.ColName<ReviewWebhookDal>(x => x.UpdatedById)} = {EntityMapper.ParameterName(nameof(row.UpdatedById))},
                {EntityMapper.ColName<ReviewWebhookDal>(x => x.Name)} = {EntityMapper.ParameterName(nameof(row.Name))},
                {EntityMapper.ColName<ReviewWebhookDal>(x => x.ExternalSystemName)} = {EntityMapper.ParameterName(nameof(row.ExternalSystemName))},
                {EntityMapper.ColName<ReviewWebhookDal>(x => x.IsActive)} = {EntityMapper.ParameterName(nameof(row.IsActive))},
                {EntityMapper.ColName<ReviewWebhookDal>(x => x.ResponsibleUserId)} = {EntityMapper.ParameterName(nameof(row.ResponsibleUserId))}
            WHERE {EntityMapper.ColName<ReviewWebhookDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(row.Id))} RETURNING *
            """;
        var saved = await _connection.QuerySingleAsync<ReviewWebhookDal>(previous is null ? insertQuery : updateQuery, row, transaction);
        var removed = oldTargets.Except(targets).ToArray();
        await UpdateWebhookTargetsAsync(saved.Id, added, removed, actor.UserId, transaction);
        return new ReviewWebhookView(saved, targets);
    }

    /// <summary>Изменяет разницу набора целей с сохранением автора неизменённых связей</summary>
    /// <param name="id">Настройка</param>
    /// <param name="added">Добавленные цели</param>
    /// <param name="removed">Удалённые цели</param>
    /// <param name="actor">Автор настройки</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    private async Task UpdateWebhookTargetsAsync(Guid id, Guid[] added, Guid[] removed, Guid actor, IDbTransaction transaction)
    {
        if (removed.Length > 0)
        {
            var deleteQuery = $"""
                DELETE FROM {EntityMapper.TbName<ReviewWebhookRiskDal>()} WHERE {EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.WebhookId)} = {EntityMapper.ParameterName(nameof(id))} AND {EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.RiskId)} = ANY({EntityMapper.ParameterName(nameof(removed))})
                """;
            await _connection.ExecuteAsync(deleteQuery, new { id, removed }, transaction);
        }
        if (added.Length > 0)
        {
            var insertQuery = $"""
                INSERT INTO {EntityMapper.TbName<ReviewWebhookRiskDal>()} ({EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.WebhookId)}, {EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.RiskId)}, {EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.LinkedAt)}, {EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.LinkedById)})
                SELECT {EntityMapper.ParameterName(nameof(id))}, target, clock_timestamp(), {EntityMapper.ParameterName(nameof(actor))} FROM unnest({EntityMapper.ParameterName(nameof(added))}) AS target
                """;
            await _connection.ExecuteAsync(insertQuery, new { id, added, actor }, transaction);
        }
    }

    /// <summary>Читает область ранее сохранённой настройки</summary>
    /// <param name="id">Настройка</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <returns>Цели настройки</returns>
    private async Task<Guid[]> ReadWebhookTargetsAsync(Guid id, IDbTransaction transaction)
    {
        var query = $"""
            SELECT {EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.RiskId)} FROM {EntityMapper.TbName<ReviewWebhookRiskDal>()} WHERE {EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.WebhookId)} = {EntityMapper.ParameterName(nameof(id))} ORDER BY {EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.RiskId)}
            """;
        return (await _connection.QueryAsync<Guid>(query, new { id }, transaction)).ToArray();
    }

    /// <summary>Читает страницу настроек с актуальным правом администратора</summary>
    /// <typeparam name="T">Тип настройки</typeparam>
    /// <param name="actor">Администратор</param>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <param name="idColumn">Ключ EntityMapper</param>
    /// <returns>Настройки</returns>
    private async Task<T[]> ListReviewSettingsAsync<T>(RiskActor actor, int offset, int limit, string idColumn)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, IdentityRights.AdminCode, transaction);
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<T>()} ORDER BY {idColumn} OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var rows = (await _connection.QueryAsync<T>(query, new { offset, limit }, transaction)).ToArray();
        transaction.Commit();
        return rows;
    }

    /// <summary>Удаляет только актуальную версию настройки, сохраняя запуски</summary>
    /// <typeparam name="T">Тип настройки</typeparam>
    /// <param name="actor">Администратор</param>
    /// <param name="id">Настройка</param>
    /// <param name="version">Ожидаемая версия</param>
    /// <param name="idColumn">Колонка ключа</param>
    /// <param name="versionColumn">Колонка версии</param>
    /// <returns>Признак удаления</returns>
    private Task<bool> DeleteReviewSettingAsync<T>(RiskActor actor, Guid id, Guid version, string idColumn, string versionColumn)
    {
        return WriteAsync(actor, IdentityRights.AdminCode, async transaction =>
        {
            var query = $"""
                DELETE FROM {EntityMapper.TbName<T>()} WHERE {idColumn} = {EntityMapper.ParameterName(nameof(id))} AND {versionColumn} = {EntityMapper.ParameterName(nameof(version))}
                """;
            if (await _connection.ExecuteAsync(query, new { id, version }, transaction) != 1)
            {
                throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
            }
            return true;
        });
    }

    /// <summary>Хеширует случайный высокоэнтропийный секрет</summary>
    /// <param name="token">Bearer-секрет</param>
    /// <returns>SHA-256 секрета</returns>
    private static byte[] HashReviewToken(string token)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }
}
