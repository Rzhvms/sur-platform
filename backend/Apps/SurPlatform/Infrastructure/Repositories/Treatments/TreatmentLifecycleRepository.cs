using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.Ports.Repositories;
using Application.UseCases.Treatments.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Migrations;
using Infrastructure.Observability;
using Infrastructure.Repositories.Reviews.Interfaces;
using Infrastructure.Repositories.Sessions.Interfaces;
using Infrastructure.Repositories.Shared;
using Infrastructure.Repositories.Treatments.Interfaces;
using Npgsql;
using System.Data;

namespace Infrastructure.Repositories.Treatments;

/// <summary>Переход риска на пересмотр после завершения мер</summary>
internal sealed class TreatmentLifecycleRepository : ClientRepository, ITreatmentLifecycleRepository
{
    /// <summary>Запуск и завершение пересмотра в общей транзакции</summary>
    private readonly IReviewStateRepository _reviewStateRepository;

    /// <summary>Инициализирует операции выбранной клиентской базы</summary>
    /// <param name="connection">Подключение выбранной клиентской базы</param>
    /// <param name="sessions">Проверка сессии и эффективных прав в транзакции</param>
    /// <param name="operations">Метрики операций базы</param>
    /// <param name="reviewStateRepository">Запуск и завершение пересмотра в общей транзакции</param>
    public TreatmentLifecycleRepository(
        IDbConnection connection,
        ITransactionalSessionRepository sessions,
        OperationsMetrics operations,
        IReviewStateRepository reviewStateRepository)
        : base(connection, sessions, operations)
    {
        _reviewStateRepository = reviewStateRepository;
    }

    /// <inheritdoc/>
    public async Task StartReviewAfterTreatmentAsync(TreatmentDal measure, bool confirmed, Guid actor, Guid operationId,
        IDbTransaction transaction, JiraSettingsDal? jira = null)
    {
        if (measure.Status == TreatmentRules.Draft)
        {
            return;
        }
        var active = TreatmentRules.ActiveStatuses();
        var draft = TreatmentRules.Draft;
        var query = $"""
            SELECT {EntityMapper.ColName<TreatmentDal>(x => x.Status)} FROM {EntityMapper.TbName<TreatmentDal>()} WHERE {EntityMapper.ColName<TreatmentDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(measure.RiskId))}
                AND {EntityMapper.ColName<TreatmentDal>(x => x.Id)} <> {EntityMapper.ParameterName(nameof(measure.Id))}
                AND ({EntityMapper.ColName<TreatmentDal>(x => x.Status)} = ANY({EntityMapper.ParameterName(nameof(active))}) OR {EntityMapper.ColName<TreatmentDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(draft))})
            """;
        var remaining = (await _connection.QueryAsync<string>(query, new { measure.RiskId, measure.Id, active, draft }, transaction)).ToArray();
        if (remaining.Any(status => active.Contains(status)))
        {
            return;
        }

        if (remaining.Length > 0 && !confirmed)
        {
            throw new IdentityCommandException(409, TreatmentRules.ConfirmationRequired);
        }

        var risk = await ReadAsync<RiskDal>(measure.RiskId, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
        TreatmentStateRepository.RequireTreatmentState(risk.Status is RiskRules.Active or RiskRules.InProgress);
        await _reviewStateRepository.StartSingleReviewAsync(risk, ReviewRules.AfterTreatment, ReviewRules.Treatment, measure.Id, actor, operationId, transaction, jira: jira);
    }
}
