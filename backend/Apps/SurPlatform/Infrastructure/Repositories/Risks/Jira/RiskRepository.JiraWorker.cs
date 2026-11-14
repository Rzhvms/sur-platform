using Microsoft.AspNetCore.DataProtection;
using Infrastructure.Observability;
using Microsoft.Extensions.Configuration;
using Infrastructure.Repositories.Risks.Models;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.UseCases.Jira.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Repositories.Identity;
using Infrastructure.Repositories.Risks.Helpers;
using Infrastructure.Services.Jira;

namespace Infrastructure.Repositories.Risks;

/// <summary>Устойчивая доставка и повторное чтение задач Jira</summary>
internal sealed partial class RiskRepository
{
    /// <summary>Классификатор обмена Jira</summary>
    private const string JiraMetricOperation = "jira";

    /// <summary>Операция Jira завершилась прикладной или транспортной ошибкой</summary>
    private const string JiraMetricError = "error";

    /// <summary>Ограничение порции задач на клиента</summary>
    private const int JiraWorkerBatch = 20;

    /// <inheritdoc/>
    public async Task ProcessJiraAsync(CancellationToken cancellation)
    {

        if (jiraConfiguration.GetValue(RecoverySettings.PauseOutbound, false))
        {
            return;
        }

        var settings = await ReadJiraAsync();
        var sending = JiraRules.Sending;
        var unknown = JiraRules.Unknown;
        var expired = DateTime.UtcNow.AddMinutes(-2);
        var stuck = $"""
            UPDATE {EntityMapper.TbName<JiraDeliveryDal>()} SET {EntityMapper.ColName<JiraDeliveryDal>(x => x.State)} = {EntityMapper.ParameterName(nameof(unknown))} WHERE {EntityMapper.ColName<JiraDeliveryDal>(x => x.State)} = {EntityMapper.ParameterName(nameof(sending))} AND {EntityMapper.ColName<JiraDeliveryDal>(x => x.StartedAt)} < {EntityMapper.ParameterName(nameof(expired))} RETURNING {EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)}
            """;
        var expiredIds = await connection.QueryAsync<Guid>(stuck, new { sending, unknown, expired });
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
        await connection.ExecuteAsync(cleanup, new { signalExpired });

        if (settings?.Enabled != true)
        {
            return;
        }
        var address = JiraRestClient.Resolve(jiraConfiguration, settings.BaseUrl);
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
        foreach (var delivery in await connection.QueryAsync<JiraDeliveryDal>(syncQuery, new { now, batch }))
        {
            cancellation.ThrowIfCancellationRequested();
            await SynchronizeJiraAsync(settings, address, token, delivery, cancellation);
        }
        foreach (var id in await connection.QueryAsync<Guid>(query, new { pending, working, inProgress, now, batch }))
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
        try
        {
            candidate = await WriteAsync<JiraCreateCandidate?>(JiraSystemActor(), null, async transaction =>
            {
                var current = await ReadJiraAsync(transaction);
                var delivery = await ReadJiraDeliveryAsync(id, transaction);
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
                await RequireValidAssignmentAsync(measure, transaction);
                var assignment = await ReadAssignmentAsync(id, transaction)
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
            if (!await jiraRest.CanAssignAsync(address, token, settings.Project, candidate.Login, cancellation))
            {
                throw new IdentityCommandException(409, TreatmentRules.InvalidExecutor);
            }
        }
        catch (Exception error) when (!cancellation.IsCancellationRequested && IsJiraFailure(error))
        {
            await RetryCreateAsync(settings, id, error is IdentityCommandException command ? command.Code : JiraRules.Unavailable);
            return;
        }
        var fields = JiraRestClient.CreateFields(settings, candidate.Measure, candidate.Login);
        var started = await WriteAsync<DateTime?>(JiraSystemActor(), null, async transaction =>
        {
            var current = await ReadJiraAsync(transaction);
            var measure = await ReadAsync<TreatmentDal>(id, EntityMapper.ColName<TreatmentDal>(x => x.Id), transaction);
            var assignment = await ReadAssignmentAsync(id, transaction);
            if (current?.Version != settings.Version || measure.Version != candidate.Measure.Version ||
                assignment is null || assignment.Id != candidate.AssignmentId || assignment.AssignedAt != candidate.AssignedAt)
            {
                return null;
            }
            if ((await ReadEligibleExecutorsAsync(measure.ProcessId, assignment.AssigneeId, 0, 1, transaction)).Length != 1)
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
            return await connection.QuerySingleOrDefaultAsync<DateTime?>(query, new { id, pending, sending }, transaction);
        });
        if (started is null)
        {
            return;
        }
        try
        {
            var issue = await jiraRest.RequestAsync(address, token, "issue", cancellation, fields);
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
            await connection.ExecuteAsync(query, new { id, sending, unknown, started });
            await LogJiraAsync(settings, id, null, JiraRules.Create, JiraRules.Unknown);
        }
    }

    /// <summary>Откладывает повтор только до сохранённого начала отправки</summary>
    /// <param name="settings">Снимок настройки</param>
    /// <param name="id">Мера</param>
    /// <param name="code">Код причины</param>
    private async Task RetryCreateAsync(JiraSettingsDal settings, Guid id, string code)
    {

        var pending = JiraRules.Pending;
        var retry = DateTime.UtcNow.AddSeconds(15);
        var query = $"""
            UPDATE {EntityMapper.TbName<JiraDeliveryDal>()} SET {EntityMapper.ColName<JiraDeliveryDal>(x => x.RetryAt)} = {EntityMapper.ParameterName(nameof(retry))} WHERE {EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))} AND {EntityMapper.ColName<JiraDeliveryDal>(x => x.State)} = {EntityMapper.ParameterName(nameof(pending))}
            """;
        if (await connection.ExecuteAsync(query, new { id, pending, retry }) > 0)
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
            return await connection.QuerySingleOrDefaultAsync<JiraDeliveryDal>(query, new { id, expected, issueId, issueKey, linked, started }, transaction)
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
        await connection.ExecuteAsync(query, row, transaction);
        operations.Outcome(JiraMetricOperation, operation == JiraRules.Create && code == JiraRules.Unknown
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
