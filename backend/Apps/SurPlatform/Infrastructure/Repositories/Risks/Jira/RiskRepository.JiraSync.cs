using Microsoft.AspNetCore.DataProtection;
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

/// <summary>Подтверждение и согласование состояния связанной задачи</summary>
internal sealed partial class RiskRepository
{
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
        var signal = await connection.QuerySingleOrDefaultAsync<JiraSignalDal>(signalQuery, new { issueId });
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
                var current = await ReadJiraAsync(transaction);
                var latest = await ReadJiraDeliveryAsync(delivery.Id, transaction);
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
                    await connection.ExecuteAsync(query, new { id, observed, issueKey }, transaction);
                }
                await CompleteJiraSyncAsync(delivery.Id, issueId, signalId, transaction);
                return accepted;
            });
            if (applied && signal is not null)
            {
                operations.Age(JiraWebhookMetric, signal.CreatedAt);
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
        await connection.ExecuteAsync(query, new { id, issueId, next }, transaction);
        if (signalId.HasValue)
        {
            await connection.ExecuteAsync(cleanup, new { issueId, signalId }, transaction);
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
        var issue = await jiraRest.RequestAsync(address, token, issuePath + Uri.EscapeDataString(target), cancellation);
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
        RequireTreatmentState(measure.Status is TreatmentRules.Working or TreatmentRules.Hold);
        var actor = IdentityAccounts.SystemUserId;
        var operationId = Guid.NewGuid();
        var previous = await ReadAssignmentAsync(id, transaction);
        if (issue.Assignee is null)
        {
            RequireTreatmentState(target != TreatmentRules.Working);
            if (previous is not null && target == TreatmentRules.Hold)
            {
                await SaveJiraAssignmentAsync(settings, id, null, previous, operationId, transaction);
            }
        }
        else
        {
            var user = ReadJiraExecutors(settings).SingleOrDefault(pair =>
                string.Equals(pair.Value, issue.Assignee, StringComparison.OrdinalIgnoreCase)).Key;
            var query = $"""
                SELECT {EntityMapper.ColName<OrgPersonDal>(x => x.Id)} FROM {EntityMapper.TbName<OrgPersonDal>()} WHERE {EntityMapper.ColName<OrgPersonDal>(x => x.UserId)} = {EntityMapper.ParameterName(nameof(user))} AND {EntityMapper.ColName<OrgPersonDal>(x => x.IsActive)}
                """;
            var assignee = await connection.QuerySingleOrDefaultAsync<Guid?>(query, new { user }, transaction);
            if (assignee is null || (await ReadEligibleExecutorsAsync(measure.ProcessId, assignee, 0, 1, transaction)).Length != 1)
            {
                throw new IdentityCommandException(409, TreatmentRules.InvalidExecutor);
            }
            if (previous?.AssigneeId != assignee)
            {
                await SaveJiraAssignmentAsync(settings, id, assignee, previous, operationId, transaction);
            }
        }
        if (target != measure.Status)
        {
            if (target == TreatmentRules.Working)
            {
                await RequireTreatmentCycleAsync(measure.RiskId, RiskRules.InProgress, transaction);
                await SelectTreatmentLinkAsync(measure.RiskId, measure.RiskProcessId, transaction);
                await RequireValidAssignmentAsync(measure, transaction);
            }
            await SaveTreatmentStatusAsync(measure, target, actor, operationId, transaction, jira: settings);
            if (target is TreatmentRules.Done or TreatmentRules.Cancelled)
            {
                await StartReviewAfterTreatmentAsync(measure, true, actor, operationId, transaction, jira: settings);
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
    private async Task SaveJiraAssignmentAsync(JiraSettingsDal settings, Guid id, Guid? assignee, TreatmentAssignmentDal? previous, Guid operationId, IDbTransaction transaction)
    {

        var row = new TreatmentAssignmentDal { Id = Guid.NewGuid(), TreatmentId = id, AssigneeId = assignee.GetValueOrDefault(),
            AssignedById = IdentityAccounts.SystemUserId, AssignmentSource = JiraRules.AssignmentSource };
        TreatmentAssignmentDal? updated = null;
        if (assignee is null)
        {
            var query = $"""
                DELETE FROM {EntityMapper.TbName<TreatmentAssignmentDal>()} WHERE {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.TreatmentId)} = {EntityMapper.ParameterName(nameof(id))}
                """;
            await connection.ExecuteAsync(query, new { id }, transaction);
        }
        else
        {
            var query = $"""
                INSERT INTO {EntityMapper.TbName<TreatmentAssignmentDal>()} ({EntityMapper.ColName<TreatmentAssignmentDal>(x => x.Id)}, {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.TreatmentId)}, {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssigneeId)}, {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignedById)}, {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignmentSource)})
                VALUES ({EntityMapper.ParameterName(nameof(row.Id))}, {EntityMapper.ParameterName(nameof(row.TreatmentId))}, {EntityMapper.ParameterName(nameof(row.AssigneeId))}, {EntityMapper.ParameterName(nameof(row.AssignedById))}, {EntityMapper.ParameterName(nameof(row.AssignmentSource))})
                ON CONFLICT ({EntityMapper.ColName<TreatmentAssignmentDal>(x => x.TreatmentId)}) DO UPDATE SET
                    {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssigneeId)} = EXCLUDED.{EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssigneeId)},
                    {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignedById)} = EXCLUDED.{EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignedById)},
                    {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignmentSource)} = EXCLUDED.{EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignmentSource)},
                    {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignedAt)} = clock_timestamp() RETURNING *
                """;
            updated = await connection.QuerySingleAsync<TreatmentAssignmentDal>(query, row, transaction);
        }
        await BusinessHistory.RecordAsync(transaction, operationId, settings.ResponsibleUserId, (updated ?? previous ?? row).Id,
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
                issue = await ReadJiraIssueAsync(settings, JiraRestClient.Resolve(jiraConfiguration, settings.BaseUrl),
                    JiraProtector().Unprotect(settings.RestSecret), target, cancellation);
            }
            catch (Exception error) when (IsJiraFailure(error) && !cancellation.IsCancellationRequested)
            {
                throw new IdentityCommandException(409, JiraRules.Conflict);
            }
        }
        return await WriteAsync(actor, IdentityRights.AdminCode, async transaction =>
        {
            var current = await ReadJiraAsync(transaction);
            var delivery = await ReadJiraDeliveryAsync(id, transaction);
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
                    UPDATE {EntityMapper.TbName<JiraDeliveryDal>()} SET {EntityMapper.ColName<JiraDeliveryDal>(x => x.State)} = {EntityMapper.ParameterName(nameof(pending))}, {EntityMapper.ColName<JiraDeliveryDal>(x => x.StartedAt)} = NULL, {EntityMapper.ColName<JiraDeliveryDal>(x => x.RetryAt)} = NULL WHERE {EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))}
                    """;
                await connection.ExecuteAsync(query, new { id, pending }, transaction);
            }
            return await ReadJiraViewAsync(measure, transaction);
        });
    }

}
