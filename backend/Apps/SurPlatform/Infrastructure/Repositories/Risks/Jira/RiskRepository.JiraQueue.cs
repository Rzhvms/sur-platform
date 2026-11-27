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

/// <summary>Очередь Jira в транзакциях мер клиента</summary>
internal sealed partial class RiskRepository
{
    /// <summary>Читает доставку меры</summary>
    /// <param name="id">Мера</param>
    /// <param name="transaction">Снимок</param>
    /// <returns>Текущая доставка</returns>
    private Task<JiraDeliveryDal?> ReadJiraDeliveryAsync(Guid id, IDbTransaction transaction)
    {
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<JiraDeliveryDal>()} WHERE {EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))}
            """;
        return connection.QuerySingleOrDefaultAsync<JiraDeliveryDal>(query, new { id }, transaction);
    }

    /// <summary>Добавляет задачу в очередь транзакции меры</summary>
    /// <param name="id">Мера</param>
    /// <param name="actor">Инициатор</param>
    /// <param name="transaction">Запуск или возобновление</param>
    /// <param name="triggeredFromRisk">Истина для запуска набора, ложь для возобновления меры</param>
    private async Task QueueJiraAsync(Guid id, Guid actor, IDbTransaction transaction, bool triggeredFromRisk)
    {
        if ((await ReadJiraAsync(transaction))?.Enabled != true)
        {
            return;
        }
        var pending = JiraRules.Pending;
        var query = $"""
            INSERT INTO {EntityMapper.TbName<JiraDeliveryDal>()} AS delivery ({EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)}, {EntityMapper.ColName<JiraDeliveryDal>(x => x.State)}, {EntityMapper.ColName<JiraDeliveryDal>(x => x.TriggeredBy)}, {EntityMapper.ColName<JiraDeliveryDal>(x => x.TriggeredFromRisk)})
            VALUES ({EntityMapper.ParameterName(nameof(id))}, {EntityMapper.ParameterName(nameof(pending))}, {EntityMapper.ParameterName(nameof(actor))}, {EntityMapper.ParameterName(nameof(triggeredFromRisk))})
            ON CONFLICT ({EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)}) DO UPDATE
            SET {EntityMapper.ColName<JiraDeliveryDal>(x => x.TriggeredBy)} = EXCLUDED.{EntityMapper.ColName<JiraDeliveryDal>(x => x.TriggeredBy)},
                {EntityMapper.ColName<JiraDeliveryDal>(x => x.TriggeredFromRisk)} = EXCLUDED.{EntityMapper.ColName<JiraDeliveryDal>(x => x.TriggeredFromRisk)},
                {EntityMapper.ColName<JiraDeliveryDal>(x => x.RetryCode)} = NULL,
                {EntityMapper.ColName<JiraDeliveryDal>(x => x.RetryAt)} = NULL
            WHERE delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.State)} = {EntityMapper.ParameterName(nameof(pending))}
            """;
        await connection.ExecuteAsync(query, new { id, pending, actor, triggeredFromRisk }, transaction);
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
        await connection.ExecuteAsync(query, new { actor, pending, working, inProgress, restored }, transaction);
        var refresh = $"""
            UPDATE {EntityMapper.TbName<JiraDeliveryDal>()} SET {EntityMapper.ColName<JiraDeliveryDal>(x => x.ObservedAt)} = NULL, {EntityMapper.ColName<JiraDeliveryDal>(x => x.SyncAt)} = NULL WHERE {EntityMapper.ColName<JiraDeliveryDal>(x => x.IssueId)} IS NOT NULL
            """;
        await connection.ExecuteAsync(refresh, transaction: transaction);
    }

    /// <summary>Определяет доступность ручного управления</summary>
    /// <param name="measure">Мера</param>
    /// <param name="transaction">Транзакция чтения</param>
    /// <param name="viewer">Пользователь карточки, если требуется персональное уведомление</param>
    /// <returns>Данные интеграции</returns>
    private async Task<JiraTreatmentView> ReadJiraViewAsync(TreatmentDal measure, IDbTransaction transaction, Guid? viewer = null)
    {
        var settings = await ReadJiraAsync(transaction);
        var delivery = await ReadJiraDeliveryAsync(measure.Id, transaction);
        var controlled = settings?.Enabled == true && (measure.Status == TreatmentRules.Working ||
            measure.Status == TreatmentRules.Hold && (delivery?.State is (JiraRules.Linked or JiraRules.Sending or JiraRules.Unknown)));
        var url = settings is not null && delivery?.IssueKey is { } key
            ? new Uri(new Uri(settings.BaseUrl), "browse/" + Uri.EscapeDataString(key)).AbsoluteUri : null;
        var state = delivery?.State;
        if (measure.Status is (TreatmentRules.Done or TreatmentRules.Cancelled) && state == JiraRules.Pending)
        {
            state = null;
        }
        if (settings?.Enabled == true && measure.Status == TreatmentRules.Hold && state is (null or JiraRules.Pending))
        {
            state = JiraRules.Deferred;
        }
        return new JiraTreatmentView(state, delivery?.IssueKey, url, !controlled,
            state == JiraRules.Unknown ? delivery?.StartedAt : null)
        {
            JiraRetryPending = settings?.Enabled == true && measure.Status == TreatmentRules.Working && viewer.HasValue &&
                delivery is { State: JiraRules.Pending, RetryCode: JiraRules.Unavailable, TriggeredFromRisk: false } &&
                delivery.TriggeredBy == viewer.Value
        };
    }

    /// <summary>Проверяет активную задержку отправки для инициатора запуска риска</summary>
    /// <param name="riskId">Открытый риск</param>
    /// <param name="viewer">Текущий пользователь</param>
    /// <param name="transaction">Тот же снимок, что и у карточки</param>
    /// <returns>Инициатору следует показать уведомление</returns>
    private Task<bool> HasRiskJiraRetryAsync(Guid riskId, Guid viewer, IDbTransaction transaction)
    {
        var pending = JiraRules.Pending;
        var unavailable = JiraRules.Unavailable;
        var working = TreatmentRules.Working;
        var query = $"""
            SELECT EXISTS (
                SELECT FROM {EntityMapper.TbName<JiraDeliveryDal>()} delivery
                JOIN {EntityMapper.TbName<TreatmentDal>()} measure
                    ON measure.{EntityMapper.ColName<TreatmentDal>(x => x.Id)} = delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)}
                WHERE measure.{EntityMapper.ColName<TreatmentDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(riskId))}
                    AND measure.{EntityMapper.ColName<TreatmentDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(working))}
                    AND delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.State)} = {EntityMapper.ParameterName(nameof(pending))}
                    AND delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.RetryCode)} = {EntityMapper.ParameterName(nameof(unavailable))}
                    AND delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.TriggeredFromRisk)} = TRUE
                    AND delivery.{EntityMapper.ColName<JiraDeliveryDal>(x => x.TriggeredBy)} = {EntityMapper.ParameterName(nameof(viewer))}
                    AND EXISTS (SELECT FROM {EntityMapper.TbName<JiraSettingsDal>()} WHERE {EntityMapper.ColName<JiraSettingsDal>(x => x.Enabled)})
            )
            """;
        return connection.ExecuteScalarAsync<bool>(query, new { riskId, viewer, pending, unavailable, working }, transaction);
    }

    /// <summary>Запрещает ручную запись при внешнем управлении</summary>
    /// <param name="measure">Мера</param>
    /// <param name="transaction">Транзакция команды</param>
    private async Task RequireJiraManualControlAsync(TreatmentDal measure, IDbTransaction transaction)
    {
        if (!(await ReadJiraViewAsync(measure, transaction)).ManualControl)
        {
            throw new IdentityCommandException(409, JiraRules.ManualBlocked);
        }
    }

    /// <inheritdoc/>
    public async Task AuthorizeJiraWebhookAsync(string secret)
    {
        VerifyJiraWebhookSecret(await ReadJiraAsync(), secret);
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
            var settings = await ReadJiraAsync(transaction);
            VerifyJiraWebhookSecret(settings, secret);
            var row = new JiraSignalDal { Id = Guid.NewGuid(), IssueId = issueId, CreatedAt = DateTime.UtcNow };
            var query = $"""
                INSERT INTO {EntityMapper.TbName<JiraSignalDal>()} ({EntityMapper.ColName<JiraSignalDal>(x => x.Id)}, {EntityMapper.ColName<JiraSignalDal>(x => x.IssueId)}, {EntityMapper.ColName<JiraSignalDal>(x => x.CreatedAt)})
                VALUES ({EntityMapper.ParameterName(nameof(row.Id))}, {EntityMapper.ParameterName(nameof(row.IssueId))}, {EntityMapper.ParameterName(nameof(row.CreatedAt))})
                ON CONFLICT ({EntityMapper.ColName<JiraSignalDal>(x => x.IssueId)}) DO UPDATE SET {EntityMapper.ColName<JiraSignalDal>(x => x.Id)} = EXCLUDED.{EntityMapper.ColName<JiraSignalDal>(x => x.Id)},
                    {EntityMapper.ColName<JiraSignalDal>(x => x.CreatedAt)} = EXCLUDED.{EntityMapper.ColName<JiraSignalDal>(x => x.CreatedAt)}
                """;

            await connection.ExecuteAsync(query, row, transaction);
            return row.Id;
        });
    }

    /// <inheritdoc/>
    public async Task<JiraErrorDal[]> ListJiraErrorsAsync(RiskActor actor, int offset, int limit)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, IdentityRights.AdminCode, transaction);
        var enabled = (await ReadJiraAsync(transaction))?.Enabled == true;
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
        var rows = (await connection.QueryAsync<JiraErrorDal>(query,
            new { offset, limit, enabled, pending, working, hold, deferred, paused, stopped, sync, linked, syncScheduled, terminalMismatch }, transaction)).ToArray();
        transaction.Commit();
        return rows;
    }
}
