using Infrastructure.Repositories.Risks.Models;
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

/// <summary>Единый отбор реестра и канбан-доски</summary>
internal sealed partial class RiskRepository
{
    /// <summary>Сортировка по времени обновления</summary>
    private const string RecentSort = "recent";

    /// <summary>Сортировка по названию</summary>
    private const string NameSort = "name";

    /// <summary>Общий отбор карточек и итогов в одном снимке</summary>
    private static readonly string FilteredRisks = $"""
            FROM {EntityMapper.TbName<RiskDal>()} r
            WHERE (CAST({EntityMapper.ParameterName(nameof(RiskDal.Id))} AS uuid) IS NULL OR r.{EntityMapper.ColName<RiskDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(RiskDal.Id))})
              AND strpos(lower(r.{EntityMapper.ColName<RiskDal>(x => x.Name)}), lower({EntityMapper.ParameterName(nameof(RiskListQuery.Search))})) > 0
              AND (CAST({EntityMapper.ParameterName(nameof(RiskListQuery.Status))} AS text) IS NULL OR r.{EntityMapper.ColName<RiskDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(RiskListQuery.Status))})
              AND (CAST({EntityMapper.ParameterName(nameof(RiskListQuery.AssetId))} AS uuid) IS NULL OR r.{EntityMapper.ColName<RiskDal>(x => x.AssetId)} = {EntityMapper.ParameterName(nameof(RiskListQuery.AssetId))})
              AND (CAST({EntityMapper.ParameterName(nameof(RiskListQuery.CategoryId))} AS uuid) IS NULL OR r.{EntityMapper.ColName<RiskDal>(x => x.CategoryId)} = {EntityMapper.ParameterName(nameof(RiskListQuery.CategoryId))})
              AND EXISTS (
                  SELECT FROM {EntityMapper.TbName<RiskProcessDal>()} l JOIN {EntityMapper.TbName<ProcessDal>()} p ON p.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = l.{EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)}
                  WHERE l.{EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)} = r.{EntityMapper.ColName<RiskDal>(x => x.Id)} AND l.{EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL
                    AND (CAST({EntityMapper.ParameterName(nameof(RiskListQuery.ProcessId))} AS uuid) IS NULL OR p.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(RiskListQuery.ProcessId))})
                    AND (CAST({EntityMapper.ParameterName(nameof(RiskListQuery.DirectorateId))} AS uuid) IS NULL OR p.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)} = {EntityMapper.ParameterName(nameof(RiskListQuery.DirectorateId))})
                    AND (CAST({EntityMapper.ParameterName(nameof(RiskListQuery.OwnerId))} AS uuid) IS NULL OR l.{EntityMapper.ColName<RiskProcessDal>(x => x.OwnerId)} = {EntityMapper.ParameterName(nameof(RiskListQuery.OwnerId))})
              )
        """;

    /// <inheritdoc/>
    public async Task<RiskRow[]> ListAsync(RiskActor actor, RiskListQuery filter)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        var result = await SelectAsync(filter, null, transaction);
        transaction.Commit();
        return result.Select(MapRow).ToArray();
    }

    /// <inheritdoc/>
    public async Task<RiskColumn[]> BoardAsync(RiskActor actor, RiskListQuery filter)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        var result = await SelectAsync(filter, null, transaction, board: true);
        var totals = await CountBoardAsync(filter, transaction);
        transaction.Commit();
        return RiskRules.BoardStatuses.Select(status =>
        {
            var items = result.Where(item => item.Status == status).Select(MapRow).ToArray();
            return new RiskColumn(status, items.LongLength, items) { TotalCount = totals.GetValueOrDefault(status) };
        }).ToArray();
    }

    /// <inheritdoc/>
    public async Task<RiskCard> GetAsync(RiskActor actor, Guid id)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        var result = await SelectAsync(new RiskListQuery(), id, transaction);
        var row = result.SingleOrDefault() ?? throw new IdentityCommandException(404, RiskRules.NotFound);
        var data = MapRow(row);
        var last = await ReadLatestAssessmentAsync(id, transaction);
        var editable = last is null && await HasRightAsync(actor, RiskRules.Edit, transaction)
            && (await HasRightAsync(actor, IdentityRights.AdminCode, transaction) ||
                data.Processes.Any(process => process.IsPrimary && process.OwnerActive && process.OwnerUserId == actor.UserId));
        var reviews = await ReadRiskReviewsAsync(id, 0, ReviewRules.CardHistoryLimit, transaction);
        var jiraRetryPending = await HasRiskJiraRetryAsync(id, actor.UserId, transaction);
        transaction.Commit();
        return new RiskCard(data, editable, data.Risk.Status == RiskRules.AwaitingReview && last is { IsActive: false } ? last : null)
        {
            Reviews = reviews,
            JiraRetryPending = jiraRetryPending
        };
    }

    /// <summary>Выполняет фильтры до постраничной выборки</summary>
    /// <param name="filter">Параметры общей выборки</param>
    /// <param name="id">Опциональная выборка одной карточки</param>
    /// <param name="transaction">Согласованный снимок</param>
    /// <param name="board">Разбить окно на шесть независимых колонок</param>
    /// <returns>Строки выбранной страницы</returns>
    private async Task<RiskReadRow[]> SelectAsync(RiskListQuery filter, Guid? id, IDbTransaction transaction, bool board = false)
    {
        var order = filter.Sort switch
        {
            null or RecentSort => $"r.{EntityMapper.ColName<RiskDal>(x => x.UpdatedAt)} DESC, r.{EntityMapper.ColName<RiskDal>(x => x.Id)}",
            NameSort => $"lower(r.{EntityMapper.ColName<RiskDal>(x => x.Name)}), r.{EntityMapper.ColName<RiskDal>(x => x.Id)}",
            _ => throw new IdentityCommandException(400, RiskRules.Invalid)
        };
        if (filter.Status is { } requestedStatus && !RiskRules.BoardStatuses.Contains(requestedStatus))
        {
            throw new IdentityCommandException(400, RiskRules.Invalid);
        }

        var search = filter.Search?.Trim() ?? string.Empty;
        var partition = board ? $"PARTITION BY r.{EntityMapper.ColName<RiskDal>(x => x.Status)}" : string.Empty;
        var offset = filter.Offset;
        var limit = filter.Limit;
        var page = $"rn > {EntityMapper.ParameterName(nameof(offset))} AND rn <= CAST({EntityMapper.ParameterName(nameof(offset))} AS bigint) + {EntityMapper.ParameterName(nameof(limit))}";
        var query = $"""
            WITH filtered AS (
                SELECT r.*, row_number() OVER ({partition} ORDER BY {order}) AS rn
                {FilteredRisks}
            ), page AS (
                SELECT * FROM filtered WHERE {page}
            )
            SELECT r.{EntityMapper.ColName<RiskDal>(x => x.Status)} AS {EntityMapper.ColName<RiskReadRow>(x => x.Status)},
                   to_jsonb(r)::text AS {EntityMapper.ColName<RiskReadRow>(x => x.RiskJson)},
                   to_jsonb(a)::text AS {EntityMapper.ColName<RiskReadRow>(x => x.AssessmentJson)},
                   links.items AS {EntityMapper.ColName<RiskReadRow>(x => x.ProcessesJson)},
                   h.{EntityMapper.ColName<HazardDal>(x => x.Name)} AS {EntityMapper.ColName<RiskReadRow>(x => x.HazardName)},
                   cat.{EntityMapper.ColName<RiskCategoryDal>(x => x.Name)} AS {EntityMapper.ColName<RiskReadRow>(x => x.CategoryName)},
                   asset.{EntityMapper.ColName<AssetDal>(x => x.Name)} AS {EntityMapper.ColName<RiskReadRow>(x => x.AssetName)},
                   author.{EntityMapper.ColName<UserProfileDal>(x => x.FullName)} AS {EntityMapper.ColName<RiskReadRow>(x => x.AuthorName)}
            FROM page r
            LEFT JOIN {EntityMapper.TbName<HazardDal>()} h ON h.{EntityMapper.ColName<HazardDal>(x => x.Id)} = r.{EntityMapper.ColName<RiskDal>(x => x.HazardId)}
            LEFT JOIN {EntityMapper.TbName<RiskCategoryDal>()} cat ON cat.{EntityMapper.ColName<RiskCategoryDal>(x => x.Id)} = r.{EntityMapper.ColName<RiskDal>(x => x.CategoryId)}
            LEFT JOIN {EntityMapper.TbName<AssetDal>()} asset ON asset.{EntityMapper.ColName<AssetDal>(x => x.Id)} = r.{EntityMapper.ColName<RiskDal>(x => x.AssetId)}
            LEFT JOIN {EntityMapper.TbName<UserProfileDal>()} author ON author.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = r.{EntityMapper.ColName<RiskDal>(x => x.AuthorId)}
            LEFT JOIN {EntityMapper.TbName<RiskAssessmentDal>()} a ON a.{EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskId)} = r.{EntityMapper.ColName<RiskDal>(x => x.Id)} AND a.{EntityMapper.ColName<RiskAssessmentDal>(x => x.IsActive)}
            LEFT JOIN LATERAL (
                SELECT jsonb_agg(to_jsonb(link_view) ORDER BY link_view.{EntityMapper.ColName<RiskProcessView>(x => x.IsPrimary)} DESC,
                    link_view.{EntityMapper.ColName<RiskProcessView>(x => x.ProcessId)})::text AS items
                FROM (
                    SELECT l.{EntityMapper.ColName<RiskProcessDal>(x => x.Id)} AS {EntityMapper.ColName<RiskProcessView>(x => x.RiskProcessId)},
                        p.{EntityMapper.ColName<ProcessDal>(x => x.Id)} AS {EntityMapper.ColName<RiskProcessView>(x => x.ProcessId)},
                        p.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)} AS {EntityMapper.ColName<RiskProcessView>(x => x.DirectorateId)},
                        l.{EntityMapper.ColName<RiskProcessDal>(x => x.OwnerId)} AS {EntityMapper.ColName<RiskProcessView>(x => x.OwnerId)},
                        l.{EntityMapper.ColName<RiskProcessDal>(x => x.IsPrimary)} AS {EntityMapper.ColName<RiskProcessView>(x => x.IsPrimary)},
                        p.{EntityMapper.ColName<ProcessDal>(x => x.Name)} AS {EntityMapper.ColName<RiskProcessView>(x => x.ProcessName)},
                        d.{EntityMapper.ColName<DirectorateDal>(x => x.Name)} AS {EntityMapper.ColName<RiskProcessView>(x => x.DirectorateName)},
                        CASE WHEN person.{EntityMapper.ColName<OrgPersonDal>(x => x.IsActive)} AND u.{EntityMapper.ColName<UserProfileDal>(x => x.IsActive)} THEN u.{EntityMapper.ColName<UserProfileDal>(x => x.FullName)} END
                            AS {EntityMapper.ColName<RiskProcessView>(x => x.OwnerName)},
                        person.{EntityMapper.ColName<OrgPersonDal>(x => x.IsActive)} AND u.{EntityMapper.ColName<UserProfileDal>(x => x.IsActive)} AS {EntityMapper.ColName<RiskProcessView>(x => x.OwnerActive)},
                        person.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)} AS {EntityMapper.ColName<RiskProcessView>(x => x.OwnerUserId)}
                    FROM {EntityMapper.TbName<RiskProcessDal>()} l JOIN {EntityMapper.TbName<ProcessDal>()} p ON p.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = l.{EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)}
                    JOIN {EntityMapper.TbName<DirectorateDal>()} d ON d.{EntityMapper.ColName<DirectorateDal>(x => x.Id)} = p.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)}
                    JOIN {EntityMapper.TbName<OrgPersonDal>()} person ON person.{EntityMapper.ColName<OrgPersonDal>(x => x.Id)} = l.{EntityMapper.ColName<RiskProcessDal>(x => x.OwnerId)}
                    JOIN {EntityMapper.TbName<UserProfileDal>()} u ON u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = person.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)}
                    WHERE l.{EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)} = r.{EntityMapper.ColName<RiskDal>(x => x.Id)} AND l.{EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL
                ) link_view
            ) links ON true
            ORDER BY r.rn, r.{EntityMapper.ColName<RiskDal>(x => x.Status)}
            """;
        var rows = await connection.QueryAsync<RiskReadRow>(query,
            new { Id = id, Search = search, filter.Status, filter.AssetId, filter.CategoryId, filter.ProcessId,
                filter.DirectorateId, filter.OwnerId, offset, limit }, transaction);
        return rows.ToArray();
    }

    /// <summary>Считает подходящие риски по статусам до пагинации</summary>
    /// <param name="filter">Те же фильтры, что использует выборка карточек</param>
    /// <param name="transaction">Общий снимок с карточками доски</param>
    /// <returns>Число совпадений по статусу</returns>
    private async Task<Dictionary<string, long>> CountBoardAsync(RiskListQuery filter, IDbTransaction transaction)
    {
        var search = filter.Search?.Trim() ?? string.Empty;
        Guid? id = null;
        var status = EntityMapper.ColName<RiskDal>(x => x.Status);
        var query = $"""
            SELECT r.{status}, count(*)
            {FilteredRisks}
            GROUP BY r.{status}
            """;
        var rows = await connection.QueryAsync<(string Status, long TotalCount)>(query,
            new { Id = id, Search = search, filter.Status, filter.AssetId, filter.CategoryId, filter.ProcessId,
                filter.DirectorateId, filter.OwnerId }, transaction);
        return rows.ToDictionary(row => row.Status, row => row.TotalCount);
    }

    /// <summary>Преобразует проекцию PostgreSQL в карточку реестра</summary>
    /// <param name="row">Строка из общего отбора</param>
    /// <returns>Паспорт, связи и исключительно текущая оценка</returns>
    private static RiskRow MapRow(RiskReadRow row)
    {
        var risk = JsonSerializer.Deserialize<RiskDal>(row.RiskJson ?? throw new InvalidOperationException(), JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException();
        var processes = row.ProcessesJson is { } links
            ? JsonSerializer.Deserialize<RiskProcessView[]>(links, JsonSerializerOptions.Web) ?? [] : [];
        var assessment = row.AssessmentJson is { } current
            ? JsonSerializer.Deserialize<RiskAssessmentDal>(current, JsonSerializerOptions.Web) : null;
        return new RiskRow(risk, processes, assessment, row.HazardName, row.CategoryName, row.AssetName, row.AuthorName);
    }
}
