using Infrastructure.Repositories.History.Helpers;
using Infrastructure.Repositories.Jira.Models;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.Ports.Repositories;
using Application.UseCases.Jira.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Clients;
using Infrastructure.Observability;
using Infrastructure.Repositories.Identity;
using Infrastructure.Repositories.Jira.Interfaces;
using Infrastructure.Repositories.Reviews.Interfaces;
using Infrastructure.Repositories.Risks.Models;
using Infrastructure.Repositories.Sessions.Interfaces;
using Infrastructure.Repositories.Shared;
using Infrastructure.Repositories.Treatments.Interfaces;
using Infrastructure.Repositories.Treatments;
using Infrastructure.Services.Jira;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Domain.DalModels.Jira;
using Domain.DalModels.Organization;
using Domain.DalModels.Risks;
using Domain.DalModels.Treatments;
using Domain.DalModels.Users;

namespace Infrastructure.Repositories.Jira;

/// <summary>Настройка и обработка интеграции Jira</summary>
internal sealed class JiraRepository : ClientRepository, IJiraRepository, IJiraWorkerRepository
{
    /// <summary>Выбранный клиентский контекст</summary>
    private readonly ClientDatabaseContext _databaseContext;

    /// <summary>Разрешенные адреса Jira</summary>
    private readonly IConfiguration _jiraConfiguration;

    /// <summary>HTTPS-клиент Jira</summary>
    private readonly JiraRestClient _jiraRest;

    /// <summary>Защита реквизитов Jira</summary>
    private readonly IDataProtectionProvider _protection;

    /// <summary>Поручения Jira и режим управления мерой</summary>
    private readonly IJiraStateRepository _jiraStateRepository;

    /// <summary>Запуск и завершение пересмотра в общей транзакции</summary>
    private readonly IReviewStateRepository _reviewStateRepository;

    /// <summary>Пересмотр после завершения последней меры</summary>
    private readonly ITreatmentLifecycleRepository _treatmentLifecycleRepository;

    /// <summary>Назначения и переходы состояния мер</summary>
    private readonly ITreatmentStateRepository _treatmentStateRepository;

    /// <summary>Инициализирует операции выбранной клиентской базы</summary>
    /// <param name="connection">Подключение выбранной клиентской базы</param>
    /// <param name="sessions">Проверка сессии и эффективных прав в транзакции</param>
    /// <param name="operations">Метрики операций базы</param>
    /// <param name="databaseContext">Выбранный клиентский контекст</param>
    /// <param name="jiraConfiguration">Разрешенные адреса Jira</param>
    /// <param name="jiraRest">HTTPS-клиент Jira</param>
    /// <param name="protection">Защита реквизитов Jira</param>
    /// <param name="jiraStateRepository">Поручения Jira и режим управления мерой</param>
    /// <param name="reviewStateRepository">Запуск и завершение пересмотра в общей транзакции</param>
    /// <param name="treatmentLifecycleRepository">Пересмотр после завершения последней меры</param>
    /// <param name="treatmentStateRepository">Назначения и переходы состояния мер</param>
    public JiraRepository(
        IDbConnection connection,
        ITransactionalSessionRepository sessions,
        OperationsMetrics operations,
        ClientDatabaseContext databaseContext,
        IConfiguration jiraConfiguration,
        JiraRestClient jiraRest,
        IDataProtectionProvider protection,
        IJiraStateRepository jiraStateRepository,
        IReviewStateRepository reviewStateRepository,
        ITreatmentLifecycleRepository treatmentLifecycleRepository,
        ITreatmentStateRepository treatmentStateRepository)
        : base(connection, sessions, operations)
    {
        _databaseContext = databaseContext;
        _jiraConfiguration = jiraConfiguration;
        _jiraRest = jiraRest;
        _protection = protection;
        _jiraStateRepository = jiraStateRepository;
        _reviewStateRepository = reviewStateRepository;
        _treatmentLifecycleRepository = treatmentLifecycleRepository;
        _treatmentStateRepository = treatmentStateRepository;
    }

    /// <summary>Восстанавливает недостающие задания для работающих мер</summary>
    /// <param name="actor">Администратор восстановления</param>
    /// <param name="restored">Включение или повторное включение обмена</param>
    /// <param name="transaction">Включение</param>
    private async Task RestoreJiraAsync(Guid actor, bool restored, IDbTransaction transaction)
    {
        var pending = JiraRules.Pending;
        var working = TreatmentRules.Working;
        var inProgress = RiskRules.InProgress;
        var query = $"""
            INSERT INTO {EntityMapper.TbName<JiraDeliveryDal>()} AS delivery ({EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)}, {EntityMapper.ColName<JiraDeliveryDal>(x => x.State)}, {EntityMapper.ColName<JiraDeliveryDal>(x => x.TriggeredBy)})
            SELECT measure.{EntityMapper.ColName<TreatmentDal>(x => x.Id)}, {EntityMapper.ParameterName(nameof(pending))}, {EntityMapper.ParameterName(nameof(actor))} FROM {EntityMapper.TbName<TreatmentDal>()} measure
            JOIN {EntityMapper.TbName<RiskDal>()} risk ON risk.{EntityMapper.ColName<RiskDal>(x => x.Id)} = measure.{EntityMapper.ColName<TreatmentDal>(x => x.RiskId)}
            WHERE measure.{EntityMapper.ColName<TreatmentDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(working))} AND risk.{EntityMapper.ColName<RiskDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(inProgress))}
            ON CONFLICT ({EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)}) DO UPDATE
            SET {EntityMapper.ColName<JiraDeliveryDal>(x => x.TriggeredBy)} = EXCLUDED.{EntityMapper.ColName<JiraDeliveryDal>(x => x.TriggeredBy)},
                {EntityMapper.ColName<JiraDeliveryDal>(x => x.TriggeredFromRisk)} = NULL,
                {EntityMapper.ColName<JiraDeliveryDal>(x => x.RetryCode)} = NULL,
                {EntityMapper.ColName<JiraDeliveryDal>(x => x.RetryAt)} = NULL
            WHERE {EntityMapper.ParameterName(nameof(restored))}
                AND delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.State)} = {EntityMapper.ParameterName(nameof(pending))}
            """;
        await _connection.ExecuteAsync(query, new { actor, pending, working, inProgress, restored }, transaction);
        var refresh = $"""
            UPDATE {EntityMapper.TbName<JiraDeliveryDal>()} SET {EntityMapper.ColName<JiraDeliveryDal>(x => x.ObservedAt)} = NULL, {EntityMapper.ColName<JiraDeliveryDal>(x => x.SyncAt)} = NULL WHERE {EntityMapper.ColName<JiraDeliveryDal>(x => x.IssueId)} IS NOT NULL
            """;
        await _connection.ExecuteAsync(refresh, transaction: transaction);
    }

    /// <inheritdoc/>
    public async Task AuthorizeJiraWebhookAsync(string secret)
    {
        VerifyJiraWebhookSecret(await _jiraStateRepository.ReadJiraAsync(), secret);
    }

    /// <summary>Проверяет активность и хеш входящего секрета</summary>
    /// <param name="settings">Настройка выбранного клиента</param>
    /// <param name="secret">Пароль Basic</param>
    /// <exception cref="IdentityCommandException">Интеграция отключена или секрет недействителен</exception>
    private static void VerifyJiraWebhookSecret(JiraSettingsDal? settings, string secret)
    {
        if (settings?.Enabled != true || !CryptographicOperations.FixedTimeEquals(settings.WebhookHash, HashJiraSecret(secret)))
        {
            throw new IdentityCommandException(401, JiraRules.Invalid);
        }
    }

    /// <inheritdoc/>
    public Task<Guid> AcceptJiraSignalAsync(string secret, string issueId)
    {
        return WriteAsync(new RiskActor(IdentityAccounts.SystemUserId, Guid.Empty, 0), null, async transaction =>
        {
            var settings = await _jiraStateRepository.ReadJiraAsync(transaction);
            VerifyJiraWebhookSecret(settings, secret);
            var row = new JiraSignalDal { Id = Guid.NewGuid(), IssueId = issueId, CreatedAt = DateTime.UtcNow };
            var query = $"""
                INSERT INTO {EntityMapper.TbName<JiraSignalDal>()} ({EntityMapper.ColName<JiraSignalDal>(x => x.Id)}, {EntityMapper.ColName<JiraSignalDal>(x => x.IssueId)}, {EntityMapper.ColName<JiraSignalDal>(x => x.CreatedAt)})
                VALUES ({EntityMapper.ParameterName(nameof(row.Id))}, {EntityMapper.ParameterName(nameof(row.IssueId))}, {EntityMapper.ParameterName(nameof(row.CreatedAt))})
                ON CONFLICT ({EntityMapper.ColName<JiraSignalDal>(x => x.IssueId)}) DO UPDATE SET {EntityMapper.ColName<JiraSignalDal>(x => x.Id)} = EXCLUDED.{EntityMapper.ColName<JiraSignalDal>(x => x.Id)},
                    {EntityMapper.ColName<JiraSignalDal>(x => x.CreatedAt)} = EXCLUDED.{EntityMapper.ColName<JiraSignalDal>(x => x.CreatedAt)}
                """;

            await _connection.ExecuteAsync(query, row, transaction);
            return row.Id;
        });
    }

    /// <inheritdoc/>
    public async Task<JiraErrorDal[]> ListJiraErrorsAsync(RiskActor actor, int offset, int limit)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, IdentityRights.AdminCode, transaction);
        var enabled = (await _jiraStateRepository.ReadJiraAsync(transaction))?.Enabled == true;
        var pending = JiraRules.Pending;
        var working = TreatmentRules.Working;
        var hold = TreatmentRules.Hold;
        var deferred = JiraRules.Deferred;
        var paused = JiraRules.RetryPaused;
        var stopped = JiraRules.RetryStopped;
        var sync = JiraRules.Sync;
        var linked = JiraRules.Linked;
        var syncScheduled = JiraRules.SyncScheduled;
        var terminalMismatch = JiraRules.TerminalMismatch;
        var state = EntityMapper.ColName<JiraDeliveryDal>(x => x.State);
        var status = EntityMapper.ColName<TreatmentDal>(x => x.Status);
        var query = $"""
            SELECT entry.*,
                CASE WHEN delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.IssueId)} = entry.{EntityMapper.ColName<JiraErrorDal>(x => x.IssueId)}
                    THEN delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.IssueKey)} END AS {EntityMapper.ColName<JiraErrorDal>(x => x.IssueKey)},
                CASE
                    WHEN entry.{EntityMapper.ColName<JiraErrorDal>(x => x.Operation)} = {EntityMapper.ParameterName(nameof(sync))}
                        AND entry.{EntityMapper.ColName<JiraErrorDal>(x => x.Code)} = {EntityMapper.ParameterName(nameof(terminalMismatch))}
                        THEN {EntityMapper.ParameterName(nameof(stopped))}
                    WHEN entry.{EntityMapper.ColName<JiraErrorDal>(x => x.Operation)} = {EntityMapper.ParameterName(nameof(sync))}
                        AND delivery.{state} = {EntityMapper.ParameterName(nameof(linked))}
                        THEN CASE WHEN {EntityMapper.ParameterName(nameof(enabled))}
                            THEN {EntityMapper.ParameterName(nameof(syncScheduled))} ELSE {EntityMapper.ParameterName(nameof(paused))} END
                    WHEN delivery.{state} = {EntityMapper.ParameterName(nameof(pending))}
                        AND measure.{status} = {EntityMapper.ParameterName(nameof(hold))} THEN {EntityMapper.ParameterName(nameof(deferred))}
                    WHEN delivery.{state} = {EntityMapper.ParameterName(nameof(pending))}
                        AND measure.{status} <> {EntityMapper.ParameterName(nameof(working))} THEN {EntityMapper.ParameterName(nameof(stopped))}
                    WHEN delivery.{state} = {EntityMapper.ParameterName(nameof(pending))}
                        AND NOT {EntityMapper.ParameterName(nameof(enabled))} THEN {EntityMapper.ParameterName(nameof(paused))}
                    ELSE delivery.{state}
                END AS {EntityMapper.ColName<JiraErrorDal>(x => x.RetryState)}
            FROM {EntityMapper.TbName<JiraErrorDal>()} entry
            LEFT JOIN {EntityMapper.TbName<JiraDeliveryDal>()} delivery
                ON delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)} = entry.{EntityMapper.ColName<JiraErrorDal>(x => x.TreatmentId)}
            LEFT JOIN {EntityMapper.TbName<TreatmentDal>()} measure
                ON measure.{EntityMapper.ColName<TreatmentDal>(x => x.Id)} = delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)}
            ORDER BY entry.{EntityMapper.ColName<JiraErrorDal>(x => x.OccurredAt)} DESC, entry.{EntityMapper.ColName<JiraErrorDal>(x => x.Id)} DESC
            OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var rows = (await _connection.QueryAsync<JiraErrorDal>(query,
            new { offset, limit, enabled, pending, working, hold, deferred, paused, stopped, sync, linked, syncScheduled, terminalMismatch }, transaction)).ToArray();
        transaction.Commit();
        return rows;
    }

    /// <inheritdoc/>
    public Task<JiraSettingsDal?> GetJiraAsync(RiskActor actor)
    {
        return WriteAsync(actor, IdentityRights.AdminCode, _jiraStateRepository.ReadJiraAsync);
    }

    /// <inheritdoc/>
    public async Task<JiraAdministrationView?> GetJiraAdministrationAsync(RiskActor actor)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, IdentityRights.AdminCode, transaction);
        var settings = await _jiraStateRepository.ReadJiraAsync(transaction);
        if (settings is null)
        {
            transaction.Commit();
            return null;
        }

        var pending = JiraRules.Pending;
        var sending = JiraRules.Sending;
        var unknown = JiraRules.Unknown;
        var unavailable = JiraRules.Unavailable;
        var working = TreatmentRules.Working;
        var userId = actor.UserId;
        var showRestoration = settings.Enabled && settings.RestoredById == userId;
        var state = EntityMapper.ColName<JiraDeliveryDal>(x => x.State);
        var query = $"""
            SELECT count(*) FILTER (
                    WHERE delivery.{state} IN ({EntityMapper.ParameterName(nameof(pending))}, {EntityMapper.ParameterName(nameof(sending))})
                    AND measure.{EntityMapper.ColName<TreatmentDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(working))}
                ),
                count(*) FILTER (WHERE delivery.{state} = {EntityMapper.ParameterName(nameof(unknown))}),
                {EntityMapper.ParameterName(nameof(showRestoration))} AND count(*) FILTER (
                    WHERE delivery.{state} = {EntityMapper.ParameterName(nameof(pending))}
                    AND measure.{EntityMapper.ColName<TreatmentDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(working))}
                    AND delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.RetryCode)} = {EntityMapper.ParameterName(nameof(unavailable))}
                    AND delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.TriggeredFromRisk)} IS NULL
                    AND delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.TriggeredBy)} = {EntityMapper.ParameterName(nameof(userId))}
                ) > 0
            FROM {EntityMapper.TbName<JiraDeliveryDal>()} delivery
            JOIN {EntityMapper.TbName<TreatmentDal>()} measure
                ON measure.{EntityMapper.ColName<TreatmentDal>(x => x.Id)} = delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)}
            """;
        var summary = await _connection.QuerySingleAsync<(long Pending, long Unknown, bool RestorationRetry)>(
            query, new { pending, sending, unknown, unavailable, working, userId, showRestoration }, transaction);
        transaction.Commit();
        return new JiraAdministrationView(settings, summary.Pending, summary.Unknown, summary.RestorationRetry);
    }

    /// <inheritdoc/>
    public async Task<JiraSettingsDal> SaveJiraAsync(RiskActor actor, JiraSettingsRequest request, CancellationToken cancellation)
    {
        var previous = await GetJiraAsync(actor);
        if ((previous?.Version ?? Guid.Empty) != request.Version)
        {
            throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
        }
        var address = JiraRestClient.Resolve(_jiraConfiguration, RequiredText(request.BaseUrl));
        var project = RequiredText(request.Project);
        var issueType = RequiredText(request.IssueType);
        if (!JiraRestClient.IsResourceId(project) || !JiraRestClient.IsResourceId(issueType))
        {
            throw new IdentityCommandException(400, JiraRules.Invalid);
        }
        var token = request.RestToken ?? (previous is not null && previous.BaseUrl == address.AbsoluteUri
            ? JiraProtector().Unprotect(previous.RestSecret) : null);
        if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsWhiteSpace) ||
            request.WebhookSecret is { Length: < JiraRules.MinWebhookSecretLength or > JiraRules.MaxWebhookSecretLength } ||
            request.Executors is null || request.Executors.Values.Any(string.IsNullOrWhiteSpace) ||
            request.Executors.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Executors.Count)
        {
            throw new IdentityCommandException(400, JiraRules.Invalid);
        }
        var hash = request.WebhookSecret is null ? previous?.WebhookHash : HashJiraSecret(request.WebhookSecret);
        if (hash is null || CryptographicOperations.FixedTimeEquals(hash, HashJiraSecret(token)))
        {
            throw new IdentityCommandException(400, JiraRules.Invalid);
        }
        var zone = previous?.TimeZone ?? _jiraConfiguration["Application:TimeZone"] ?? TimeZoneInfo.Utc.Id;
        _ = TimeZoneInfo.FindSystemTimeZoneById(zone);
        var restore = request.Enabled && previous?.Enabled != true;
        var row = new JiraSettingsDal
        {
            Id = Guid.Empty, Version = Guid.NewGuid(), BaseUrl = address.AbsoluteUri, Project = project,
            IssueType = issueType, DoneStatus = request.DoneStatus, HoldStatus = request.HoldStatus,
            CancelStatus = request.CancelStatus, Enabled = request.Enabled, ResponsibleUserId = request.ResponsibleUserId,
            CreatedById = previous is null ? actor.UserId : previous.CreatedById,
            RestoredById = restore ? actor.UserId : previous?.RestoredById,
            UpdatedById = actor.UserId, RestSecret = JiraProtector().Protect(token), WebhookHash = hash, TimeZone = zone,
            Executors = JsonSerializer.Serialize(request.Executors)
        };
        if (row.Enabled)
        {
            await ValidateJiraAsync(row, token, cancellation);
            await VerifyJiraExecutorMappingsAsync(row, token, cancellation);
        }

        return await WriteAsync(actor, IdentityRights.AdminCode, async transaction =>
        {
            var current = await _jiraStateRepository.ReadJiraAsync(transaction);
            if ((current?.Version ?? Guid.Empty) != request.Version)
            {
                throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
            }
            if (current is not null && (current.BaseUrl != row.BaseUrl || current.Project != row.Project || current.IssueType != row.IssueType))
            {
                var sending = JiraRules.Sending;
                var unknown = JiraRules.Unknown;
                var used = $"""
                    SELECT EXISTS (SELECT FROM {EntityMapper.TbName<JiraDeliveryDal>()} WHERE {EntityMapper.ColName<JiraDeliveryDal>(x => x.IssueId)} IS NOT NULL OR {EntityMapper.ColName<JiraDeliveryDal>(x => x.State)} IN ({EntityMapper.ParameterName(nameof(sending))}, {EntityMapper.ParameterName(nameof(unknown))}))
                    """;
                if (await _connection.ExecuteScalarAsync<bool>(used, new { sending, unknown }, transaction))
                {
                    throw new IdentityCommandException(409, JiraRules.Conflict);
                }
            }
            await _reviewStateRepository.RequireReviewResponsibleAsync(row.ResponsibleUserId, transaction);
            var query = $"""
                INSERT INTO {EntityMapper.TbName<JiraSettingsDal>()} ({EntityMapper.ColName<JiraSettingsDal>(x => x.Id)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.Version)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.BaseUrl)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.Project)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.IssueType)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.DoneStatus)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.HoldStatus)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.CancelStatus)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.Enabled)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.ResponsibleUserId)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.UpdatedById)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.RestSecret)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.WebhookHash)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.TimeZone)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.Executors)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.CreatedById)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.RestoredById)})
                VALUES ({EntityMapper.ParameterName(nameof(row.Id))}, {EntityMapper.ParameterName(nameof(row.Version))}, {EntityMapper.ParameterName(nameof(row.BaseUrl))}, {EntityMapper.ParameterName(nameof(row.Project))}, {EntityMapper.ParameterName(nameof(row.IssueType))}, {EntityMapper.ParameterName(nameof(row.DoneStatus))}, {EntityMapper.ParameterName(nameof(row.HoldStatus))}, {EntityMapper.ParameterName(nameof(row.CancelStatus))}, {EntityMapper.ParameterName(nameof(row.Enabled))}, {EntityMapper.ParameterName(nameof(row.ResponsibleUserId))}, {EntityMapper.ParameterName(nameof(row.UpdatedById))}, {EntityMapper.ParameterName(nameof(row.RestSecret))}, {EntityMapper.ParameterName(nameof(row.WebhookHash))}, {EntityMapper.ParameterName(nameof(row.TimeZone))}, {EntityMapper.ParameterName(nameof(row.Executors))}, {EntityMapper.ParameterName(nameof(row.CreatedById))}, {EntityMapper.ParameterName(nameof(row.RestoredById))})
                ON CONFLICT ({EntityMapper.ColName<JiraSettingsDal>(x => x.Id)}) DO UPDATE SET {EntityMapper.ColName<JiraSettingsDal>(x => x.Version)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.Version)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.BaseUrl)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.BaseUrl)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.Project)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.Project)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.IssueType)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.IssueType)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.DoneStatus)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.DoneStatus)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.HoldStatus)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.HoldStatus)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.CancelStatus)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.CancelStatus)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.Enabled)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.Enabled)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.ResponsibleUserId)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.ResponsibleUserId)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.UpdatedById)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.UpdatedById)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.RestSecret)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.RestSecret)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.WebhookHash)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.WebhookHash)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.TimeZone)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.TimeZone)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.Executors)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.Executors)},
                    {EntityMapper.ColName<JiraSettingsDal>(x => x.RestoredById)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.RestoredById)}
                """;
            await _connection.ExecuteAsync(query, row, transaction);
            if (row.Enabled)
            {
                await RestoreJiraAsync(actor.UserId, restore, transaction);
            }
            return row;
        });
    }

    /// <inheritdoc/>
    public async Task<JsonElement> DiscoverJiraAsync(RiskActor actor, JiraDiscoveryRequest request, CancellationToken cancellation)
    {
        var previous = await GetJiraAsync(actor);
        var address = JiraRestClient.Resolve(_jiraConfiguration, RequiredText(request.BaseUrl));
        var token = request.RestToken ?? (previous is not null && previous.BaseUrl == address.AbsoluteUri
            ? JiraProtector().Unprotect(previous.RestSecret) : null);
        if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsWhiteSpace))
        {
            throw new IdentityCommandException(400, JiraRules.Invalid);
        }
        try
        {
            const string projectsPath = "project";
            if (request.Project is not { } key)
            {
                var projects = await _jiraRest.RequestAsync(address, token, projectsPath, cancellation);
                return JsonSerializer.SerializeToElement(new
                {
                    projects = projects.EnumerateArray().Select(project => new
                    {
                        id = project.GetProperty("id").GetString(),
                        key = project.GetProperty("key").GetString(),
                        name = project.GetProperty("name").GetString()
                    })
                });
            }
            if (!JiraRestClient.IsResourceId(key))
            {
                throw new IdentityCommandException(400, JiraRules.Invalid);
            }
            var projectId = Uri.EscapeDataString(key);
            var issueTypes = await _jiraRest.MetadataAsync(address, token, $"issue/createmeta/{projectId}/issuetypes", cancellation);
            var statuses = await _jiraRest.RequestAsync(address, token, $"{projectsPath}/{projectId}/statuses", cancellation);
            return JsonSerializer.SerializeToElement(new { issueTypes, statuses });
        }
        catch (Exception error) when (error is not IdentityCommandException && !cancellation.IsCancellationRequested && IsJiraFailure(error))
        {
            throw new IdentityCommandException(400, JiraRules.Unavailable);
        }
    }

    /// <summary>Проверяет поддерживаемые поля, статусы и право создавать задачи</summary>
    /// <param name="row">Предлагаемая настройка</param>
    /// <param name="token">Секрет доступа</param>
    /// <param name="cancellation">Отмена</param>
    private async Task ValidateJiraAsync(JiraSettingsDal row, string token, CancellationToken cancellation)
    {

        try
        {
            var uri = JiraRestClient.Resolve(_jiraConfiguration, row.BaseUrl);
            var project = Uri.EscapeDataString(row.Project);
            var issueType = Uri.EscapeDataString(row.IssueType);
            var prefix = $"issue/createmeta/{project}/issuetypes";
            var types = await _jiraRest.MetadataAsync(uri, token, prefix, cancellation);
            if (!types.Any(type => type.GetProperty("id").GetString() == row.IssueType &&
                (!type.TryGetProperty("subtask", out var subtask) || !subtask.GetBoolean())))
            {
                throw new IdentityCommandException(400, JiraRules.Invalid);
            }
            var fields = await _jiraRest.MetadataAsync(uri, token, prefix + "/" + issueType, cancellation);
            var names = new HashSet<string>(StringComparer.Ordinal) { "project", "issuetype", "summary", "assignee", "duedate" };
            var available = fields.Select(field => field.GetProperty("fieldId").GetString()).ToHashSet();
            if (!names.IsSubsetOf(available) || !available.Contains(JiraRestClient.DescriptionField) ||
                fields.Any(field => field.GetProperty("required").GetBoolean() &&
                !names.Contains(field.GetProperty("fieldId").GetString() ?? string.Empty) &&
                (!field.TryGetProperty("hasDefaultValue", out var hasDefault) || !hasDefault.GetBoolean())))
            {
                throw new IdentityCommandException(400, JiraRules.Invalid);
            }
            var statuses = await _jiraRest.RequestAsync(uri, token, $"project/{project}/statuses", cancellation);
            var ids = statuses.EnumerateArray().Single(type => type.GetProperty("id").GetString() == row.IssueType)
                .GetProperty("statuses").EnumerateArray().Select(status => status.GetProperty("id").GetString()).ToHashSet();
            var selected = new[] { row.DoneStatus, row.HoldStatus, row.CancelStatus }.OfType<string>().ToArray();
            var permissions = await _jiraRest.RequestAsync(uri, token,
                $"mypermissions?projectKey={project}&permissions=BROWSE_PROJECTS,CREATE_ISSUES,ASSIGN_ISSUES", cancellation);
            if (string.IsNullOrWhiteSpace(row.DoneStatus) || selected.Distinct().Count() != selected.Length ||
                !ids.IsSupersetOf(selected) || new[] { "BROWSE_PROJECTS", "CREATE_ISSUES", "ASSIGN_ISSUES" }.Any(permission =>
                    !permissions.GetProperty("permissions").GetProperty(permission).GetProperty("havePermission").GetBoolean()))
            {
                throw new IdentityCommandException(400, JiraRules.Invalid);
            }
        }
        catch (Exception error) when (!cancellation.IsCancellationRequested && error is
            HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new IdentityCommandException(400, JiraRules.Invalid);
        }
    }

    /// <summary>Подтверждает соответствия по рабочему email и проверяет назначаемость</summary>
    /// <param name="settings">Настройки и предлагаемые соответствия</param>
    /// <param name="token">REST-секрет</param>
    /// <param name="cancellation">Отмена проверки</param>
    private async Task VerifyJiraExecutorMappingsAsync(JiraSettingsDal settings, string token, CancellationToken cancellation)
    {
        var mappings = ReadJiraExecutors(settings);
        if (mappings.Count == 0)
        {
            return;
        }

        var ids = mappings.Keys.ToArray();
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<UserProfileDal>()}
            WHERE {EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = ANY({EntityMapper.ParameterName(nameof(ids))})
              AND {EntityMapper.ColName<UserProfileDal>(x => x.IsActive)}
            """;
        var users = (await _connection.QueryAsync<UserProfileDal>(query, new { ids })).ToArray();
        if (users.Length != ids.Length)
        {
            throw new IdentityCommandException(400, JiraRules.Invalid);
        }
        var address = JiraRestClient.Resolve(_jiraConfiguration, settings.BaseUrl);
        try
        {
            foreach (var user in users)
            {
                var emailAddress = RequiredText(user.Email);
                var candidates = await _jiraRest.RequestAsync(address, token, JiraRestClient.UserSearchPath + Uri.EscapeDataString(emailAddress), cancellation);
                var exact = candidates.EnumerateArray().Where(candidate => candidate.GetProperty("active").GetBoolean()
                    && candidate.TryGetProperty("emailAddress", out var email)
                    && string.Equals(email.GetString(), user.Email, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (exact.Length != 1 || exact[0].GetProperty("name").GetString() != mappings[user.UserId]
                    || !await _jiraRest.CanAssignAsync(address, token, settings.Project, mappings[user.UserId], cancellation))
                {
                    throw new IdentityCommandException(400, JiraRules.Invalid);
                }
            }
        }
        catch (Exception error) when (error is not IdentityCommandException && !cancellation.IsCancellationRequested && IsJiraFailure(error))
        {
            throw new IdentityCommandException(400, JiraRules.Invalid);
        }
    }

    /// <summary>Привязывает шифрование REST-реквизитов к клиентскому хосту</summary>
    /// <returns>Защита секрета клиента</returns>
    private IDataProtector JiraProtector()
    {
        return _protection.CreateProtector(JiraRules.Source, _databaseContext.Client.Host);
    }

    /// <summary>Хеширует пароль входящей схемы Basic</summary>
    /// <param name="secret">Пароль вебхука</param>
    /// <returns>Хеш пароля</returns>
    private static byte[] HashJiraSecret(string secret)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    /// <summary>Задержка от записи webhook до применения нового наблюдения</summary>
    private const string JiraWebhookMetric = "jira.webhook";

    /// <summary>Перечитывает связанную задачу и применяет наблюдение один раз</summary>
    /// <param name="settings">Снимок настройки</param>
    /// <param name="address">Разрешённый адрес</param>
    /// <param name="token">REST-секрет</param>
    /// <param name="delivery">Подтверждённая связь</param>
    /// <param name="cancellation">Отмена</param>
    private async Task SynchronizeJiraAsync(JiraSettingsDal settings, Uri address, string token, JiraDeliveryDal delivery, CancellationToken cancellation)
    {

        var issueId = delivery.IssueId;
        if (issueId is null)
        {
            return;
        }
        var signalQuery = $"""
            SELECT * FROM {EntityMapper.TbName<JiraSignalDal>()}
            WHERE {EntityMapper.ColName<JiraSignalDal>(x => x.IssueId)} = {EntityMapper.ParameterName(nameof(issueId))}
            """;
        var signal = await _connection.QuerySingleOrDefaultAsync<JiraSignalDal>(signalQuery, new { issueId });
        var signalId = signal?.Id;
        try
        {
            var issue = await ReadJiraIssueAsync(settings, address, token, issueId, cancellation);
            if (issue.Id != issueId)
            {
                throw new IdentityCommandException(409, JiraRules.Conflict);
            }
            var applied = await WriteAsync(JiraSystemActor(), null, async transaction =>
            {
                var current = await _jiraStateRepository.ReadJiraAsync(transaction);
                var latest = await _jiraStateRepository.ReadJiraDeliveryAsync(delivery.Id, transaction);
                if (current?.Version != settings.Version || latest?.IssueId != issueId)
                {
                    return false;
                }
                var observed = issue.UpdatedAt.ToUniversalTime();
                var changed = latest is not null && (latest.ObservedAt is null || latest.ObservedAt < observed);
                var accepted = changed;
                if (changed)
                {
                    try
                    {
                        await ApplyJiraIssueAsync(settings, delivery.Id, issue, transaction);
                    }
                    catch (IdentityCommandException error) when (error.Code == JiraRules.TerminalMismatch)
                    {
                        await LogJiraAsync(settings, delivery.Id, issueId, JiraRules.Sync, error.Code, transaction);
                        accepted = false;
                    }
                    var id = delivery.Id;
                    var issueKey = issue.Key;
                    var query = $"""
                        UPDATE {EntityMapper.TbName<JiraDeliveryDal>()}
                        SET {EntityMapper.ColName<JiraDeliveryDal>(x => x.ObservedAt)} = {EntityMapper.ParameterName(nameof(observed))},
                            {EntityMapper.ColName<JiraDeliveryDal>(x => x.IssueKey)} = {EntityMapper.ParameterName(nameof(issueKey))}
                        WHERE {EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))}
                        """;
                    await _connection.ExecuteAsync(query, new { id, observed, issueKey }, transaction);
                }
                await CompleteJiraSyncAsync(delivery.Id, issueId, signalId, transaction);
                return accepted;
            });
            if (applied && signal is not null)
            {
                _operations.Age(JiraWebhookMetric, signal.CreatedAt);
            }
        }
        catch (Exception error) when (!cancellation.IsCancellationRequested &&
            (IsJiraFailure(error) || error is Npgsql.PostgresException))
        {
            await LogJiraAsync(settings, delivery.Id, issueId, JiraRules.Sync,
                error is IdentityCommandException command ? command.Code : JiraRules.Unavailable);
            await CompleteJiraSyncAsync(delivery.Id, issueId, signalId);
        }
    }

    /// <summary>Назначает контрольное чтение и удаляет только обработанные сигналы</summary>
    /// <param name="id">Мера</param>
    /// <param name="issueId">Задача</param>
    /// <param name="signalId">Сигнал, прочитанный перед обращением к Jira</param>
    /// <param name="transaction">Транзакция применения</param>
    private async Task CompleteJiraSyncAsync(Guid id, string issueId, Guid? signalId, IDbTransaction? transaction = null)
    {

        var next = DateTime.UtcNow.AddSeconds(25);
        var query = $"""
            UPDATE {EntityMapper.TbName<JiraDeliveryDal>()} SET {EntityMapper.ColName<JiraDeliveryDal>(x => x.SyncAt)} = {EntityMapper.ParameterName(nameof(next))} WHERE {EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))} AND {EntityMapper.ColName<JiraDeliveryDal>(x => x.IssueId)} = {EntityMapper.ParameterName(nameof(issueId))}
            """;
        var cleanup = $"""
            DELETE FROM {EntityMapper.TbName<JiraSignalDal>()} WHERE {EntityMapper.ColName<JiraSignalDal>(x => x.IssueId)} = {EntityMapper.ParameterName(nameof(issueId))} AND {EntityMapper.ColName<JiraSignalDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(signalId))}
            """;
        await _connection.ExecuteAsync(query, new { id, issueId, next }, transaction);
        if (signalId.HasValue)
        {
            await _connection.ExecuteAsync(cleanup, new { issueId, signalId }, transaction);
        }
    }

    /// <summary>Читает и проверяет проект, тип, версию и поля Jira</summary>
    /// <param name="settings">Снимок настройки</param>
    /// <param name="address">Разрешённый адрес</param>
    /// <param name="token">REST-секрет</param>
    /// <param name="target">Ключ или ID задачи</param>
    /// <param name="cancellation">Отмена</param>
    /// <returns>Проверенное наблюдение</returns>
    private async Task<JiraIssueSnapshot> ReadJiraIssueAsync(JiraSettingsDal settings, Uri address, string token, string target, CancellationToken cancellation)
    {

        if (!JiraRestClient.IsResourceId(target))
        {
            throw new IdentityCommandException(400, JiraRules.Invalid);
        }
        const string issuePath = "issue/";
        var issue = await _jiraRest.RequestAsync(address, token, issuePath + Uri.EscapeDataString(target), cancellation);
        var fields = issue.GetProperty("fields");
        if (fields.GetProperty("project").GetProperty("key").GetString() != settings.Project ||
            fields.GetProperty("issuetype").GetProperty("id").GetString() != settings.IssueType)
        {
            throw new IdentityCommandException(409, JiraRules.Conflict);
        }
        if (!JiraRestClient.IsResourceId(RequiredText(issue.GetProperty("key").GetString())))
        {
            throw new IdentityCommandException(409, JiraRules.Conflict);
        }
        var assignee = fields.GetProperty("assignee");
        var updated = RequiredText(fields.GetProperty("updated").GetString());
        if (updated.Length > 5 && updated[^5] is '+' or '-' && updated[^3] != ':')
        {
            updated = updated.Insert(updated.Length - 2, ":");
        }
        return new JiraIssueSnapshot(RequiredText(issue.GetProperty("id").GetString()), RequiredText(issue.GetProperty("key").GetString()),
            RequiredText(fields.GetProperty("status").GetProperty("id").GetString()),
            assignee.ValueKind == JsonValueKind.Null ? null : assignee.GetProperty("name").GetString(),
            DateTimeOffset.Parse(updated, System.Globalization.CultureInfo.InvariantCulture),
            fields.GetProperty("summary").GetString(), fields.GetProperty("description").GetString());
    }

    /// <summary>Применяет внешнее состояние через ограничения мер</summary>
    /// <param name="settings">Снимок настройки</param>
    /// <param name="id">Мера</param>
    /// <param name="issue">Подтверждённое наблюдение</param>
    /// <param name="transaction">Транзакция бизнес-команды</param>
    private async Task ApplyJiraIssueAsync(JiraSettingsDal settings, Guid id, JiraIssueSnapshot issue, IDbTransaction transaction)
    {

        var measure = await ReadAsync<TreatmentDal>(id, EntityMapper.ColName<TreatmentDal>(x => x.Id), transaction);
        var target = issue.Status == settings.DoneStatus ? TreatmentRules.Done
            : issue.Status == settings.HoldStatus ? TreatmentRules.Hold
            : issue.Status == settings.CancelStatus ? TreatmentRules.Cancelled : TreatmentRules.Working;
        if (measure.Status is TreatmentRules.Done or TreatmentRules.Cancelled)
        {
            if (target != measure.Status)
            {
                throw new IdentityCommandException(409, JiraRules.TerminalMismatch);
            }
            return;
        }
        TreatmentStateRepository.RequireTreatmentState(measure.Status is TreatmentRules.Working or TreatmentRules.Hold);
        var actor = IdentityAccounts.SystemUserId;
        var operationId = Guid.NewGuid();
        var previous = await _treatmentStateRepository.ReadAssignmentAsync(id, transaction);
        if (issue.Assignee is null)
        {
            throw new IdentityCommandException(409, TreatmentRules.InvalidExecutor);
        }
        else
        {
            var user = ReadJiraExecutors(settings).SingleOrDefault(pair =>
                string.Equals(pair.Value, issue.Assignee, StringComparison.OrdinalIgnoreCase)).Key;
            var query = $"""
                SELECT {EntityMapper.ColName<OrgPersonDal>(x => x.Id)} FROM {EntityMapper.TbName<OrgPersonDal>()} WHERE {EntityMapper.ColName<OrgPersonDal>(x => x.UserId)} = {EntityMapper.ParameterName(nameof(user))} AND {EntityMapper.ColName<OrgPersonDal>(x => x.IsActive)}
                """;
            var assignee = await _connection.QuerySingleOrDefaultAsync<Guid?>(query, new { user }, transaction);
            if (assignee is not Guid assigneeId || (await _treatmentStateRepository.ReadEligibleExecutorsAsync(measure.ProcessId, assigneeId, 0, 1, transaction)).Length != 1)
            {
                throw new IdentityCommandException(409, TreatmentRules.InvalidExecutor);
            }
            if (previous?.AssigneeId != assignee)
            {
                await SaveJiraAssignmentAsync(settings, id, assigneeId, previous, operationId, transaction);
            }
        }
        if (target != measure.Status)
        {
            if (target == TreatmentRules.Working)
            {
                await _treatmentStateRepository.RequireTreatmentCycleAsync(measure.RiskId, RiskRules.InProgress, transaction);
                await _treatmentStateRepository.SelectTreatmentLinkAsync(measure.RiskId, measure.RiskProcessId, transaction);
                await _treatmentStateRepository.RequireValidAssignmentAsync(measure, transaction);
            }
            await _treatmentStateRepository.SaveTreatmentStatusAsync(measure, target, actor, operationId, transaction, jira: settings);
            if (target is TreatmentRules.Done or TreatmentRules.Cancelled)
            {
                await _treatmentLifecycleRepository.StartReviewAfterTreatmentAsync(measure, true, actor, operationId, transaction, jira: settings);
            }
        }
    }

    /// <summary>Сохраняет внешнего исполнителя и общий журнал</summary>
    /// <param name="settings">Снимок настройки</param>
    /// <param name="id">Мера</param>
    /// <param name="assignee">Новый сотрудник, если назначен</param>
    /// <param name="previous">Предыдущее назначение</param>
    /// <param name="operationId">Идентификатор общей команды</param>
    /// <param name="transaction">Транзакция применения</param>
    private async Task SaveJiraAssignmentAsync(JiraSettingsDal settings, Guid id, Guid assignee, TreatmentAssignmentDal? previous, Guid operationId, IDbTransaction transaction)
    {

        var row = new TreatmentAssignmentDal { Id = Guid.NewGuid(), TreatmentId = id, AssigneeId = assignee,
            AssignedById = IdentityAccounts.SystemUserId, AssignmentSource = JiraRules.AssignmentSource };
        var query = $"""
            INSERT INTO {EntityMapper.TbName<TreatmentAssignmentDal>()} ({EntityMapper.ColName<TreatmentAssignmentDal>(x => x.Id)}, {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.TreatmentId)}, {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssigneeId)}, {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignedById)}, {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignmentSource)})
            VALUES ({EntityMapper.ParameterName(nameof(row.Id))}, {EntityMapper.ParameterName(nameof(row.TreatmentId))}, {EntityMapper.ParameterName(nameof(row.AssigneeId))}, {EntityMapper.ParameterName(nameof(row.AssignedById))}, {EntityMapper.ParameterName(nameof(row.AssignmentSource))})
            ON CONFLICT ({EntityMapper.ColName<TreatmentAssignmentDal>(x => x.TreatmentId)}) DO UPDATE SET
                {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssigneeId)} = EXCLUDED.{EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssigneeId)},
                {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignedById)} = EXCLUDED.{EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignedById)},
                {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignmentSource)} = EXCLUDED.{EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignmentSource)},
                {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignedAt)} = clock_timestamp() RETURNING *
            """;
        var updated = await _connection.QuerySingleAsync<TreatmentAssignmentDal>(query, row, transaction);
        await BusinessHistory.RecordAsync(transaction, operationId, settings.ResponsibleUserId, updated.Id,
            previous, updated, system: true, automaticSource: JiraRules.Source, configurationAuthorId: settings.UpdatedById);
    }

    /// <inheritdoc/>
    public async Task<JiraTreatmentView> ResolveJiraAsync(RiskActor actor, Guid id, JiraResolveRequest request, CancellationToken cancellation)
    {

        var settings = await GetJiraAsync(actor) ?? throw new IdentityCommandException(409, JiraRules.Conflict);
        var target = request.Issue?.Trim();
        var hasIssue = !string.IsNullOrEmpty(target);
        if (request.AttemptStartedAt is null || request.ConfirmAbsent == hasIssue)
        {
            throw new IdentityCommandException(400, JiraRules.Invalid);
        }
        JiraIssueSnapshot? issue = null;
        if (!string.IsNullOrEmpty(target))
        {
            try
            {
                issue = await ReadJiraIssueAsync(settings, JiraRestClient.Resolve(_jiraConfiguration, settings.BaseUrl),
                    JiraProtector().Unprotect(settings.RestSecret), target, cancellation);
            }
            catch (Exception error) when (IsJiraFailure(error) && !cancellation.IsCancellationRequested)
            {
                throw new IdentityCommandException(409, JiraRules.Conflict);
            }
        }
        return await WriteAsync(actor, IdentityRights.AdminCode, async transaction =>
        {
            var current = await _jiraStateRepository.ReadJiraAsync(transaction);
            var delivery = await _jiraStateRepository.ReadJiraDeliveryAsync(id, transaction);
            if (current?.Version != settings.Version || delivery is null || delivery.State != JiraRules.Unknown
                || delivery.StartedAt != request.AttemptStartedAt?.UtcDateTime)
            {
                throw new IdentityCommandException(409, JiraRules.Conflict);
            }
            var measure = await ReadAsync<TreatmentDal>(id, EntityMapper.ColName<TreatmentDal>(x => x.Id), transaction);
            if (issue is not null)
            {
                if (issue.Summary != measure.Title || (issue.Description ?? string.Empty) != (measure.Description ?? string.Empty))
                {
                    throw new IdentityCommandException(409, JiraRules.Conflict);
                }
                await LinkJiraIssueAsync(id, JiraRules.Unknown, issue.Id, issue.Key, transaction);
            }
            else
            {
                var pending = JiraRules.Pending;
                var query = $"""
                    UPDATE {EntityMapper.TbName<JiraDeliveryDal>()} SET {EntityMapper.ColName<JiraDeliveryDal>(x => x.State)} = {EntityMapper.ParameterName(nameof(pending))}, {EntityMapper.ColName<JiraDeliveryDal>(x => x.StartedAt)} = NULL, {EntityMapper.ColName<JiraDeliveryDal>(x => x.RetryAt)} = NULL,
                        {EntityMapper.ColName<JiraDeliveryDal>(x => x.RetryCode)} = NULL WHERE {EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))}
                    """;
                await _connection.ExecuteAsync(query, new { id, pending }, transaction);
            }
            return await _jiraStateRepository.ReadJiraViewAsync(measure, transaction);
        });
    }

    /// <summary>Классификатор обмена Jira</summary>
    private const string JiraMetricOperation = "jira";

    /// <summary>Операция Jira завершилась прикладной или транспортной ошибкой</summary>
    private const string JiraMetricError = "error";

    /// <summary>Ограничение порции задач на клиента</summary>
    private const int JiraWorkerBatch = 20;

    /// <inheritdoc/>
    public async Task ProcessJiraAsync(CancellationToken cancellation)
    {

        if (_jiraConfiguration.GetValue(RecoverySettings.PauseOutbound, false))
        {
            return;
        }

        var settings = await _jiraStateRepository.ReadJiraAsync();
        var sending = JiraRules.Sending;
        var unknown = JiraRules.Unknown;
        var expired = DateTime.UtcNow.AddMinutes(-2);
        var stuck = $"""
            UPDATE {EntityMapper.TbName<JiraDeliveryDal>()} SET {EntityMapper.ColName<JiraDeliveryDal>(x => x.State)} = {EntityMapper.ParameterName(nameof(unknown))} WHERE {EntityMapper.ColName<JiraDeliveryDal>(x => x.State)} = {EntityMapper.ParameterName(nameof(sending))} AND {EntityMapper.ColName<JiraDeliveryDal>(x => x.StartedAt)} < {EntityMapper.ParameterName(nameof(expired))} RETURNING {EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)}
            """;
        var expiredIds = await _connection.QueryAsync<Guid>(stuck, new { sending, unknown, expired });
        if (settings is not null)
        {
            foreach (var id in expiredIds)
            {
                await LogJiraAsync(settings, id, null, JiraRules.Create, JiraRules.Unknown);
            }
        }
        var signalExpired = DateTime.UtcNow.AddDays(-1);
        var cleanup = $"""
            DELETE FROM {EntityMapper.TbName<JiraSignalDal>()}
            WHERE {EntityMapper.ColName<JiraSignalDal>(x => x.CreatedAt)} < {EntityMapper.ParameterName(nameof(signalExpired))}
            """;
        await _connection.ExecuteAsync(cleanup, new { signalExpired });

        if (settings?.Enabled != true)
        {
            return;
        }
        var address = JiraRestClient.Resolve(_jiraConfiguration, settings.BaseUrl);
        var token = JiraProtector().Unprotect(settings.RestSecret);
        var pending = JiraRules.Pending;
        var working = TreatmentRules.Working;
        var inProgress = RiskRules.InProgress;
        var batch = JiraWorkerBatch;
        var now = DateTime.UtcNow;
        var query = $"""
            SELECT delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)} FROM {EntityMapper.TbName<JiraDeliveryDal>()} delivery
            JOIN {EntityMapper.TbName<TreatmentDal>()} measure ON measure.{EntityMapper.ColName<TreatmentDal>(x => x.Id)} = delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)}
            JOIN {EntityMapper.TbName<RiskDal>()} risk ON risk.{EntityMapper.ColName<RiskDal>(x => x.Id)} = measure.{EntityMapper.ColName<TreatmentDal>(x => x.RiskId)}
            WHERE delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.State)} = {EntityMapper.ParameterName(nameof(pending))} AND measure.{EntityMapper.ColName<TreatmentDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(working))} AND risk.{EntityMapper.ColName<RiskDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(inProgress))}
                AND (delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.RetryAt)} IS NULL OR delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.RetryAt)} <= {EntityMapper.ParameterName(nameof(now))})
            ORDER BY delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.RetryAt)} NULLS FIRST, delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)} LIMIT {EntityMapper.ParameterName(nameof(batch))}
            """;
        var syncQuery = $"""
            SELECT delivery.* FROM {EntityMapper.TbName<JiraDeliveryDal>()} delivery
            LEFT JOIN {EntityMapper.TbName<JiraSignalDal>()} signal ON signal.{EntityMapper.ColName<JiraSignalDal>(x => x.IssueId)} = delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.IssueId)}
            WHERE delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.IssueId)} IS NOT NULL
            AND (delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.SyncAt)} IS NULL OR delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.SyncAt)} <= {EntityMapper.ParameterName(nameof(now))} OR signal.{EntityMapper.ColName<JiraSignalDal>(x => x.CreatedAt)} <= {EntityMapper.ParameterName(nameof(now))})
            ORDER BY (signal.{EntityMapper.ColName<JiraSignalDal>(x => x.Id)} IS NOT NULL) DESC, delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.SyncAt)} NULLS FIRST, delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)} LIMIT {EntityMapper.ParameterName(nameof(batch))}
            """;
        foreach (var delivery in await _connection.QueryAsync<JiraDeliveryDal>(syncQuery, new { now, batch }))
        {
            cancellation.ThrowIfCancellationRequested();
            await SynchronizeJiraAsync(settings, address, token, delivery, cancellation);
        }
        foreach (var id in await _connection.QueryAsync<Guid>(query, new { pending, working, inProgress, now, batch }))
        {
            cancellation.ThrowIfCancellationRequested();
            await CreateJiraIssueAsync(settings, address, token, id, cancellation);
        }
    }

    /// <summary>Проверяет исполнителя до сохранения начала POST</summary>
    /// <param name="settings">Снимок настройки</param>
    /// <param name="address">Разрешённый адрес</param>
    /// <param name="token">REST-секрет</param>
    /// <param name="id">Мера</param>
    /// <param name="cancellation">Отмена</param>
    private async Task CreateJiraIssueAsync(JiraSettingsDal settings, Uri address, string token, Guid id, CancellationToken cancellation)
    {

        JiraCreateCandidate? candidate;
        Guid? checkedMeasureVersion = null;
        try
        {
            candidate = await WriteAsync<JiraCreateCandidate?>(JiraSystemActor(), null, async transaction =>
            {
                var current = await _jiraStateRepository.ReadJiraAsync(transaction);
                var delivery = await _jiraStateRepository.ReadJiraDeliveryAsync(id, transaction);
                var measure = await ReadAsync<TreatmentDal>(id, EntityMapper.ColName<TreatmentDal>(x => x.Id), transaction);
                if (current?.Version != settings.Version || delivery?.State != JiraRules.Pending ||
                    delivery.RetryAt > DateTime.UtcNow || measure.Status != TreatmentRules.Working)
                {
                    return null;
                }
                var risk = await ReadAsync<RiskDal>(measure.RiskId, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
                if (risk.Status != RiskRules.InProgress)
                {
                    return null;
                }
                checkedMeasureVersion = measure.Version;
                await _treatmentStateRepository.RequireValidAssignmentAsync(measure, transaction);
                var assignment = await _treatmentStateRepository.ReadAssignmentAsync(id, transaction)
                    ?? throw new IdentityCommandException(409, TreatmentRules.InvalidExecutor);
                var person = await ReadAsync<OrgPersonDal>(assignment.AssigneeId, EntityMapper.ColName<OrgPersonDal>(x => x.Id), transaction);
                var map = ReadJiraExecutors(settings);
                if (!map.TryGetValue(person.UserId, out var assignee))
                {
                    throw new IdentityCommandException(409, TreatmentRules.InvalidExecutor);
                }
                return new JiraCreateCandidate(measure, assignment.Id, assignment.AssignedAt, assignee);
            });
            if (candidate is null)
            {
                return;
            }
            if (!await _jiraRest.CanAssignAsync(address, token, settings.Project, candidate.Login, cancellation))
            {
                throw new IdentityCommandException(409, TreatmentRules.InvalidExecutor);
            }
        }
        catch (Exception error) when (!cancellation.IsCancellationRequested && IsJiraFailure(error))
        {
            if (checkedMeasureVersion is Guid measureVersion)
            {
                await RetryCreateAsync(settings, id, measureVersion, error is IdentityCommandException command ? command.Code : JiraRules.Unavailable);
            }
            return;
        }
        var fields = JiraRestClient.CreateFields(settings, candidate.Measure, candidate.Login);
        var started = await WriteAsync<DateTime?>(JiraSystemActor(), null, async transaction =>
        {
            var current = await _jiraStateRepository.ReadJiraAsync(transaction);
            var measure = await ReadAsync<TreatmentDal>(id, EntityMapper.ColName<TreatmentDal>(x => x.Id), transaction);
            var assignment = await _treatmentStateRepository.ReadAssignmentAsync(id, transaction);
            if (current?.Version != settings.Version || measure.Version != candidate.Measure.Version ||
                assignment is null || assignment.Id != candidate.AssignmentId || assignment.AssignedAt != candidate.AssignedAt)
            {
                return null;
            }
            if ((await _treatmentStateRepository.ReadEligibleExecutorsAsync(measure.ProcessId, assignment.AssigneeId, 0, 1, transaction)).Length != 1)
            {
                return null;
            }
            var pending = JiraRules.Pending;
            var sending = JiraRules.Sending;
            var query = $"""
                UPDATE {EntityMapper.TbName<JiraDeliveryDal>()} SET {EntityMapper.ColName<JiraDeliveryDal>(x => x.State)} = {EntityMapper.ParameterName(nameof(sending))}, {EntityMapper.ColName<JiraDeliveryDal>(x => x.StartedAt)} = clock_timestamp()
                WHERE {EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))} AND {EntityMapper.ColName<JiraDeliveryDal>(x => x.State)} = {EntityMapper.ParameterName(nameof(pending))}
                AND ({EntityMapper.ColName<JiraDeliveryDal>(x => x.RetryAt)} IS NULL OR {EntityMapper.ColName<JiraDeliveryDal>(x => x.RetryAt)} <= clock_timestamp())
                RETURNING {EntityMapper.ColName<JiraDeliveryDal>(x => x.StartedAt)}
                """;
            return await _connection.QuerySingleOrDefaultAsync<DateTime?>(query, new { id, pending, sending }, transaction);
        });
        if (started is null)
        {
            return;
        }
        try
        {
            var issue = await _jiraRest.RequestAsync(address, token, "issue", cancellation, fields);
            var issueId = RequiredText(issue.GetProperty("id").GetString());
            var issueKey = RequiredText(issue.GetProperty("key").GetString());
            if (!JiraRestClient.IsResourceId(issueKey) || !JiraRestClient.IsResourceId(issueId))
            {
                throw new IdentityCommandException(409, JiraRules.Conflict);
            }
            await WriteAsync(JiraSystemActor(), null, transaction =>
                LinkJiraIssueAsync(id, JiraRules.Sending, issueId, issueKey, transaction, started));
        }
        catch (Exception) when (!cancellation.IsCancellationRequested)
        {
            var sending = JiraRules.Sending;
            var unknown = JiraRules.Unknown;
            var query = $"""
                UPDATE {EntityMapper.TbName<JiraDeliveryDal>()} SET {EntityMapper.ColName<JiraDeliveryDal>(x => x.State)} = {EntityMapper.ParameterName(nameof(unknown))} WHERE {EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))} AND {EntityMapper.ColName<JiraDeliveryDal>(x => x.State)} = {EntityMapper.ParameterName(nameof(sending))} AND {EntityMapper.ColName<JiraDeliveryDal>(x => x.StartedAt)} = {EntityMapper.ParameterName(nameof(started))}
                """;
            await _connection.ExecuteAsync(query, new { id, sending, unknown, started });
            await LogJiraAsync(settings, id, null, JiraRules.Create, JiraRules.Unknown);
        }
    }

    /// <summary>Откладывает повтор, если заказ и настройка остались теми же после предварительной проверки</summary>
    /// <param name="settings">Снимок настройки</param>
    /// <param name="id">Мера</param>
    /// <param name="measureVersion">Проверенная версия меры</param>
    /// <param name="code">Код причины</param>
    private async Task RetryCreateAsync(JiraSettingsDal settings, Guid id, Guid measureVersion, string code)
    {
        var pending = JiraRules.Pending;
        var retry = DateTime.UtcNow.AddSeconds(15);
        var logged = await WriteAsync(JiraSystemActor(), null, async transaction =>
        {
            if ((await _jiraStateRepository.ReadJiraAsync(transaction))?.Version != settings.Version)
            {
                return false;
            }

            var query = $"""
                UPDATE {EntityMapper.TbName<JiraDeliveryDal>()}
                SET {EntityMapper.ColName<JiraDeliveryDal>(x => x.RetryAt)} = {EntityMapper.ParameterName(nameof(retry))},
                    {EntityMapper.ColName<JiraDeliveryDal>(x => x.RetryCode)} = {EntityMapper.ParameterName(nameof(code))}
                WHERE {EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))}
                    AND {EntityMapper.ColName<JiraDeliveryDal>(x => x.State)} = {EntityMapper.ParameterName(nameof(pending))}
                    AND EXISTS (
                        SELECT FROM {EntityMapper.TbName<TreatmentDal>()}
                        WHERE {EntityMapper.ColName<TreatmentDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))}
                            AND {EntityMapper.ColName<TreatmentDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(measureVersion))}
                    )
                """;
            return await _connection.ExecuteAsync(query, new { id, pending, retry, code, measureVersion }, transaction) > 0;
        });
        if (logged)
        {
            await LogJiraAsync(settings, id, null, JiraRules.Create, code);
        }
    }

    /// <summary>Сохраняет подтверждённую связь по ожидаемой попытке</summary>
    /// <param name="id">Мера</param>
    /// <param name="expected">Ожидаемое состояние</param>
    /// <param name="issueId">ID Jira</param>
    /// <param name="issueKey">Ключ Jira</param>
    /// <param name="transaction">Транзакция подтверждения</param>
    /// <param name="started">Идентификатор по времени попытки</param>
    /// <returns>Подтверждённая связь</returns>
    private async Task<JiraDeliveryDal> LinkJiraIssueAsync(Guid id, string expected, string issueId, string issueKey, IDbTransaction transaction, DateTime? started = null)
    {

        var linked = JiraRules.Linked;
        var query = $"""
            UPDATE {EntityMapper.TbName<JiraDeliveryDal>()} SET {EntityMapper.ColName<JiraDeliveryDal>(x => x.State)} = {EntityMapper.ParameterName(nameof(linked))}, {EntityMapper.ColName<JiraDeliveryDal>(x => x.IssueId)} = {EntityMapper.ParameterName(nameof(issueId))},
                {EntityMapper.ColName<JiraDeliveryDal>(x => x.IssueKey)} = {EntityMapper.ParameterName(nameof(issueKey))}, {EntityMapper.ColName<JiraDeliveryDal>(x => x.SyncAt)} = NULL
            WHERE {EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))} AND {EntityMapper.ColName<JiraDeliveryDal>(x => x.State)} = {EntityMapper.ParameterName(nameof(expected))}
            AND (CAST({EntityMapper.ParameterName(nameof(started))} AS timestamptz) IS NULL OR {EntityMapper.ColName<JiraDeliveryDal>(x => x.StartedAt)} = {EntityMapper.ParameterName(nameof(started))})
            RETURNING *
            """;
        try
        {
            return await _connection.QuerySingleOrDefaultAsync<JiraDeliveryDal>(query, new { id, expected, issueId, issueKey, linked, started }, transaction)
                ?? throw new IdentityCommandException(409, JiraRules.Conflict);
        }
        catch (Npgsql.PostgresException error) when (error.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
        {
            throw new IdentityCommandException(409, JiraRules.Conflict);
        }
    }

    /// <summary>Фиксирует безопасное описание ошибки и ответственного</summary>
    /// <param name="settings">Снимок настройки</param>
    /// <param name="treatmentId">Мера</param>
    /// <param name="issueId">Задача</param>
    /// <param name="operation">Операция</param>
    /// <param name="code">Причина</param>
    /// <param name="transaction">Общая транзакция при сохранении наблюдения</param>
    private async Task LogJiraAsync(JiraSettingsDal settings, Guid? treatmentId, string? issueId, string operation, string code,
        IDbTransaction? transaction = null)
    {

        var row = new JiraErrorDal
        {
            Id = Guid.NewGuid(), TreatmentId = treatmentId, IssueId = issueId, Operation = operation, Code = code,
            OccurredAt = DateTime.UtcNow, ResponsibleUserId = settings.ResponsibleUserId, ConfigurationAuthorId = settings.UpdatedById
        };
        var query = $"""
            INSERT INTO {EntityMapper.TbName<JiraErrorDal>()} ({EntityMapper.ColName<JiraErrorDal>(x => x.Id)}, {EntityMapper.ColName<JiraErrorDal>(x => x.TreatmentId)}, {EntityMapper.ColName<JiraErrorDal>(x => x.IssueId)}, {EntityMapper.ColName<JiraErrorDal>(x => x.Operation)}, {EntityMapper.ColName<JiraErrorDal>(x => x.Code)}, {EntityMapper.ColName<JiraErrorDal>(x => x.OccurredAt)}, {EntityMapper.ColName<JiraErrorDal>(x => x.ResponsibleUserId)}, {EntityMapper.ColName<JiraErrorDal>(x => x.ConfigurationAuthorId)})
            VALUES ({EntityMapper.ParameterName(nameof(row.Id))}, {EntityMapper.ParameterName(nameof(row.TreatmentId))}, {EntityMapper.ParameterName(nameof(row.IssueId))}, {EntityMapper.ParameterName(nameof(row.Operation))}, {EntityMapper.ParameterName(nameof(row.Code))}, {EntityMapper.ParameterName(nameof(row.OccurredAt))}, {EntityMapper.ParameterName(nameof(row.ResponsibleUserId))}, {EntityMapper.ParameterName(nameof(row.ConfigurationAuthorId))})
            """;
        await _connection.ExecuteAsync(query, row, transaction);
        _operations.Outcome(JiraMetricOperation, operation == JiraRules.Create && code == JiraRules.Unknown
            ? JiraRules.Unknown : JiraMetricError);
    }

    /// <summary>Создаёт системного участника без входа по JWT</summary>
    /// <returns>Технический участник</returns>
    private static RiskActor JiraSystemActor()
    {
        return new RiskActor(IdentityAccounts.SystemUserId, Guid.Empty, 0);
    }

    /// <summary>Читает сохранённое сопоставление пользователей</summary>
    /// <param name="settings">Настройка</param>
    /// <returns>Сопоставление учётных записей</returns>
    private static Dictionary<Guid, string> ReadJiraExecutors(JiraSettingsDal settings)
    {
        return JsonSerializer.Deserialize<Dictionary<Guid, string>>(settings.Executors) ?? [];
    }

    /// <summary>Отделяет ошибки REST и бизнес-проверок от отмены службы</summary>
    /// <param name="error">Причина сбоя</param>
    /// <returns>Классифицируемая ошибка</returns>
    private static bool IsJiraFailure(Exception error)
    {
        return error is IdentityCommandException or HttpRequestException or JsonException or InvalidOperationException
            or KeyNotFoundException or OperationCanceledException or FormatException;
    }
}
