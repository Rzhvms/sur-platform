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
    private async Task QueueJiraAsync(Guid id, Guid actor, IDbTransaction transaction)
    {
        if ((await ReadJiraAsync(transaction))?.Enabled != true)
        {
            return;
        }
        var pending = JiraRules.Pending;
        var query = $"""
            INSERT INTO {EntityMapper.TbName<JiraDeliveryDal>()} ({EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)}, {EntityMapper.ColName<JiraDeliveryDal>(x => x.State)}, {EntityMapper.ColName<JiraDeliveryDal>(x => x.TriggeredBy)})
            VALUES ({EntityMapper.ParameterName(nameof(id))}, {EntityMapper.ParameterName(nameof(pending))}, {EntityMapper.ParameterName(nameof(actor))}) ON CONFLICT ({EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)}) DO NOTHING
            """;
        await connection.ExecuteAsync(query, new { id, pending, actor }, transaction);
    }

    /// <summary>Восстанавливает недостающие задания для работающих мер</summary>
    /// <param name="actor">Администратор восстановления</param>
    /// <param name="transaction">Включение</param>
    private async Task RestoreJiraAsync(Guid actor, IDbTransaction transaction)
    {
        var pending = JiraRules.Pending;
        var working = TreatmentRules.Working;
        var inProgress = RiskRules.InProgress;
        var query = $"""
            INSERT INTO {EntityMapper.TbName<JiraDeliveryDal>()} ({EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)}, {EntityMapper.ColName<JiraDeliveryDal>(x => x.State)}, {EntityMapper.ColName<JiraDeliveryDal>(x => x.TriggeredBy)})
            SELECT measure.{EntityMapper.ColName<TreatmentDal>(x => x.Id)}, {EntityMapper.ParameterName(nameof(pending))}, {EntityMapper.ParameterName(nameof(actor))} FROM {EntityMapper.TbName<TreatmentDal>()} measure
            JOIN {EntityMapper.TbName<RiskDal>()} risk ON risk.{EntityMapper.ColName<RiskDal>(x => x.Id)} = measure.{EntityMapper.ColName<TreatmentDal>(x => x.RiskId)}
            WHERE measure.{EntityMapper.ColName<TreatmentDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(working))} AND risk.{EntityMapper.ColName<RiskDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(inProgress))}
            ON CONFLICT ({EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)}) DO NOTHING
            """;
        await connection.ExecuteAsync(query, new { actor, pending, working, inProgress }, transaction);
        var refresh = $"""
            UPDATE {EntityMapper.TbName<JiraDeliveryDal>()} SET {EntityMapper.ColName<JiraDeliveryDal>(x => x.ObservedAt)} = NULL, {EntityMapper.ColName<JiraDeliveryDal>(x => x.SyncAt)} = NULL WHERE {EntityMapper.ColName<JiraDeliveryDal>(x => x.IssueId)} IS NOT NULL
            """;
        await connection.ExecuteAsync(refresh, transaction: transaction);
    }

    /// <summary>Определяет доступность ручного управления</summary>
    /// <param name="measure">Мера</param>
    /// <param name="transaction">Транзакция чтения</param>
    /// <returns>Данные интеграции</returns>
    private async Task<JiraTreatmentView> ReadJiraViewAsync(TreatmentDal measure, IDbTransaction transaction)
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
            state == JiraRules.Unknown ? delivery?.StartedAt : null);
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
    public Task<JiraErrorDal[]> ListJiraErrorsAsync(RiskActor actor, int offset, int limit)
    {
        return WriteAsync(actor, IdentityRights.AdminCode, async transaction =>
        {
            var query = $"""
                SELECT * FROM {EntityMapper.TbName<JiraErrorDal>()} ORDER BY {EntityMapper.ColName<JiraErrorDal>(x => x.OccurredAt)} DESC, {EntityMapper.ColName<JiraErrorDal>(x => x.Id)} DESC OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
                """;
            return (await connection.QueryAsync<JiraErrorDal>(query, new { offset, limit }, transaction)).ToArray();
        });
    }

}
