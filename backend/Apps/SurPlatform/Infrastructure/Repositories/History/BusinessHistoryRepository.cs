using Infrastructure.Repositories.History.Helpers;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.Ports.Repositories;
using Application.UseCases.Risks.Dto;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Observability;
using Infrastructure.Repositories.History;
using Infrastructure.Repositories.Sessions.Interfaces;
using Infrastructure.Repositories.Shared;
using System.Data;
using System.Text.Json;
using Domain.DalModels.History;

namespace Infrastructure.Repositories.History;

/// <summary>Чтение административной истории</summary>
internal sealed class BusinessHistoryRepository : ClientRepository, IBusinessHistoryRepository
{
    /// <summary>Инициализирует операции выбранной клиентской базы</summary>
    /// <param name="connection">Подключение выбранной клиентской базы</param>
    /// <param name="sessions">Проверка сессии и эффективных прав в транзакции</param>
    /// <param name="operations">Метрики операций базы</param>
    public BusinessHistoryRepository(
        IDbConnection connection,
        ITransactionalSessionRepository sessions,
        OperationsMetrics operations)
        : base(connection, sessions, operations)
    {
    }

    /// <inheritdoc/>
    public async Task<JsonElement[]> HistoryAsync(RiskActor actor, BusinessPeriod period, int offset, int limit)
    {
        var (from, until) = BusinessHistoryQuery.Boundaries(period);
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, IdentityRights.AdminCode, transaction);
        var query = $"""
            SELECT events.{EntityMapper.ColName<BusinessChangeDal>(x => x.Payload)}::text FROM ({BusinessHistoryQuery.JournalSource}) events
            WHERE events.{EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)} >= {EntityMapper.ParameterName(nameof(from))} AND events.{EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)} < {EntityMapper.ParameterName(nameof(until))}
            ORDER BY events.{EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)}, events.{EntityMapper.ColName<BusinessChangeDal>(x => x.EventId)} OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var payloads = await _connection.QueryAsync<string>(query, new { from, until, offset, limit }, transaction);
        transaction.Commit();
        return payloads.Select(payload => JsonSerializer.Deserialize<JsonElement>(payload)).ToArray();
    }

    /// <inheritdoc/>
    public Task<BusinessExportDal> RequestExportAsync(RiskActor actor, BusinessPeriod period)
    {
        var (from, until) = BusinessHistoryQuery.Boundaries(period);
        return WriteAsync(actor, IdentityRights.AdminCode, async transaction =>
        {
            var actorId = actor.UserId;
            var query = $"""
                INSERT INTO {EntityMapper.TbName<BusinessExportDal>()} ({EntityMapper.ColName<BusinessExportDal>(x => x.Id)}, {EntityMapper.ColName<BusinessExportDal>(x => x.From)}, {EntityMapper.ColName<BusinessExportDal>(x => x.To)}, {EntityMapper.ColName<BusinessExportDal>(x => x.ActorId)})
                VALUES (gen_random_uuid(), {EntityMapper.ParameterName(nameof(period.From))},
                    {EntityMapper.ParameterName(nameof(period.To))}, {EntityMapper.ParameterName(nameof(actorId))})
                RETURNING {EntityMapper.ColName<BusinessExportDal>(x => x.Id)},
                    CAST({EntityMapper.ColName<BusinessExportDal>(x => x.From)} AS timestamp) AS {EntityMapper.ColName<BusinessExportDal>(x => x.From)},
                    CAST({EntityMapper.ColName<BusinessExportDal>(x => x.To)} AS timestamp) AS {EntityMapper.ColName<BusinessExportDal>(x => x.To)},
                    {EntityMapper.ColName<BusinessExportDal>(x => x.ActorId)}, {EntityMapper.ColName<BusinessExportDal>(x => x.CreatedAt)}
                """;
            var export = await _connection.QuerySingleAsync<BusinessExportDal>(query, new
            {
                From = period.From.ToDateTime(TimeOnly.MinValue),
                To = period.To.ToDateTime(TimeOnly.MinValue),
                actorId
            }, transaction);
            var exportId = export.Id;
            var snapshotQuery = $"""
                INSERT INTO {EntityMapper.TbName<BusinessExportRowDal>()} ({EntityMapper.ColName<BusinessExportRowDal>(x => x.ExportId)}, {EntityMapper.ColName<BusinessExportRowDal>(x => x.EventId)}, {EntityMapper.ColName<BusinessExportRowDal>(x => x.Payload)})
                SELECT {EntityMapper.ParameterName(nameof(exportId))}, events.{EntityMapper.ColName<BusinessChangeDal>(x => x.EventId)}, events.{EntityMapper.ColName<BusinessChangeDal>(x => x.Payload)}
                FROM ({BusinessHistoryQuery.JournalSource}) events
                WHERE events.{EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)} >= {EntityMapper.ParameterName(nameof(from))} AND events.{EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)} < {EntityMapper.ParameterName(nameof(until))}
                """;
            await _connection.ExecuteAsync(snapshotQuery, new { exportId, from, until }, transaction);
            return export;
        });
    }
}
