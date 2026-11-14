using System.Data;
using System.Text.Json;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.UseCases.Risks.Dto;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;

namespace Infrastructure.Repositories.Risks;

/// <summary>Административное чтение журнала и фиксация экспортного снимка</summary>
internal sealed partial class RiskRepository
{
    /// <summary>Единая проекция обработанных событий и ещё ожидающих поручений</summary>
    private static readonly string JournalSource = $"""
        SELECT {EntityMapper.ColName<BusinessChangeDal>(x => x.EventId)}, {EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)}, {EntityMapper.ColName<BusinessChangeDal>(x => x.Payload)} FROM {EntityMapper.TbName<BusinessChangeDal>()}
        UNION ALL
        SELECT j.{EntityMapper.ColName<BusinessChangeJobDal>(x => x.EventId)}, j.{EntityMapper.ColName<BusinessChangeJobDal>(x => x.OccurredAt)}, j.{EntityMapper.ColName<BusinessChangeJobDal>(x => x.Payload)} FROM {EntityMapper.TbName<BusinessChangeJobDal>()} j
        WHERE NOT EXISTS (SELECT FROM {EntityMapper.TbName<BusinessChangeDal>()} e WHERE e.{EntityMapper.ColName<BusinessChangeDal>(x => x.EventId)} = j.{EntityMapper.ColName<BusinessChangeJobDal>(x => x.EventId)})
        """;

    /// <inheritdoc/>
    public async Task<JsonElement[]> HistoryAsync(RiskActor actor, BusinessPeriod period, int offset, int limit)
    {
        var (from, until) = Boundaries(period);
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, IdentityRights.AdminCode, transaction);
        var query = $"""
            SELECT events.{EntityMapper.ColName<BusinessChangeDal>(x => x.Payload)}::text FROM ({JournalSource}) events
            WHERE events.{EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)} >= {EntityMapper.ParameterName(nameof(from))} AND events.{EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)} < {EntityMapper.ParameterName(nameof(until))}
            ORDER BY events.{EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)}, events.{EntityMapper.ColName<BusinessChangeDal>(x => x.EventId)} OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var payloads = await connection.QueryAsync<string>(query, new { from, until, offset, limit }, transaction);
        transaction.Commit();
        return payloads.Select(payload => JsonSerializer.Deserialize<JsonElement>(payload)).ToArray();
    }

    /// <inheritdoc/>
    public Task<BusinessExportDal> RequestExportAsync(RiskActor actor, BusinessPeriod period)
    {
        var (from, until) = Boundaries(period);
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
            var export = await connection.QuerySingleAsync<BusinessExportDal>(query, new
            {
                From = period.From.ToDateTime(TimeOnly.MinValue),
                To = period.To.ToDateTime(TimeOnly.MinValue),
                actorId
            }, transaction);
            var exportId = export.Id;
            var snapshotQuery = $"""
                INSERT INTO {EntityMapper.TbName<BusinessExportRowDal>()} ({EntityMapper.ColName<BusinessExportRowDal>(x => x.ExportId)}, {EntityMapper.ColName<BusinessExportRowDal>(x => x.EventId)}, {EntityMapper.ColName<BusinessExportRowDal>(x => x.Payload)})
                SELECT {EntityMapper.ParameterName(nameof(exportId))}, events.{EntityMapper.ColName<BusinessChangeDal>(x => x.EventId)}, events.{EntityMapper.ColName<BusinessChangeDal>(x => x.Payload)}
                FROM ({JournalSource}) events
                WHERE events.{EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)} >= {EntityMapper.ParameterName(nameof(from))} AND events.{EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)} < {EntityMapper.ParameterName(nameof(until))}
                """;
            await connection.ExecuteAsync(snapshotQuery, new { exportId, from, until }, transaction);
            return export;
        });
    }

    /// <summary>Преобразует включительные даты в полуоткрытый UTC-интервал</summary>
    /// <param name="period">Даты запроса</param>
    /// <returns>Первая полночь и исключённая полночь следующего дня</returns>
    private static (DateTime From, DateTime Until) Boundaries(BusinessPeriod period)
    {
        if (period.From == default || period.To < period.From || period.To == DateOnly.MaxValue)
        {
            throw new IdentityCommandException(400, RiskRules.Invalid);
        }

        return (period.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            period.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    }
}
