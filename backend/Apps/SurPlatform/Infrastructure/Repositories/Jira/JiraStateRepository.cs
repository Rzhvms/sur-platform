using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.Ports.Repositories;
using Application.UseCases.Jira.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Observability;
using Infrastructure.Repositories.Identity;
using Infrastructure.Repositories.Jira.Interfaces;
using Infrastructure.Repositories.Sessions.Interfaces;
using Infrastructure.Repositories.Shared;
using Infrastructure.Services.Jira;
using Microsoft.AspNetCore.DataProtection;
using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Domain.DalModels.Jira;
using Domain.DalModels.Treatments;

namespace Infrastructure.Repositories.Jira;

/// <summary>Настройка и обработка интеграции Jira</summary>
internal sealed class JiraStateRepository : ClientRepository, IJiraStateRepository
{
    /// <summary>Инициализирует операции выбранной клиентской базы</summary>
    /// <param name="connection">Подключение выбранной клиентской базы</param>
    /// <param name="sessions">Проверка сессии и эффективных прав в транзакции</param>
    /// <param name="operations">Метрики операций базы</param>
    public JiraStateRepository(
        IDbConnection connection,
        ITransactionalSessionRepository sessions,
        OperationsMetrics operations)
        : base(connection, sessions, operations)
    {
    }

    /// <inheritdoc/>
    public Task<JiraDeliveryDal?> ReadJiraDeliveryAsync(Guid id, IDbTransaction transaction)
    {
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<JiraDeliveryDal>()} WHERE {EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))}
            """;
        return _connection.QuerySingleOrDefaultAsync<JiraDeliveryDal>(query, new { id }, transaction);
    }

    /// <inheritdoc/>
    public async Task QueueJiraAsync(Guid id, Guid actor, IDbTransaction transaction, bool triggeredFromRisk)
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
        await _connection.ExecuteAsync(query, new { id, pending, actor, triggeredFromRisk }, transaction);
    }

    /// <inheritdoc/>
    public async Task<JiraTreatmentView> ReadJiraViewAsync(TreatmentDal measure, IDbTransaction transaction, Guid? viewer = null)
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

    /// <inheritdoc/>
    public Task<bool> HasRiskJiraRetryAsync(Guid riskId, Guid viewer, IDbTransaction transaction)
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
        return _connection.ExecuteScalarAsync<bool>(query, new { riskId, viewer, pending, unavailable, working }, transaction);
    }

    /// <inheritdoc/>
    public async Task RequireJiraManualControlAsync(TreatmentDal measure, IDbTransaction transaction)
    {
        if (!(await ReadJiraViewAsync(measure, transaction)).ManualControl)
        {
            throw new IdentityCommandException(409, JiraRules.ManualBlocked);
        }
    }

    /// <inheritdoc/>
    public Task<JiraSettingsDal?> ReadJiraAsync(IDbTransaction? transaction = null)
    {
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<JiraSettingsDal>()}
            """;
        return _connection.QuerySingleOrDefaultAsync<JiraSettingsDal>(query, transaction: transaction);
    }
}
