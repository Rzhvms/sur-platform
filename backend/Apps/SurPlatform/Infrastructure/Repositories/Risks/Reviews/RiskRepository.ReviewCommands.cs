using System.Data;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.UseCases.Risks.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Repositories.Risks.Helpers;

namespace Infrastructure.Repositories.Risks;

/// <summary>Передача риска и чтение истории пересмотра</summary>
internal sealed partial class RiskRepository
{
    /// <inheritdoc/>
    public Task<RiskDal> TransferRiskAsync(RiskActor actor, Guid id, TransferRiskRequest request)
    {
        return WriteAsync(actor, ReviewRules.TransferRight, async transaction =>
        {
            await RequirePrimaryOwnerAsync(actor, id, transaction);
            var risk = await ReadAsync<RiskDal>(id, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
            RequireVersion(risk.Version, request.Version);
            var current = await ReadCurrentAssessmentAsync(id, transaction);
            if (risk.Status != RiskRules.Active || current is not { ResponseMode: null })
            {
                throw new IdentityCommandException(409, RiskRules.InvalidTransition);
            }

            var links = await ReadActiveRiskLinksAsync(id, transaction);
            if (request.ProcessId == Guid.Empty || links.Any(link => link.ProcessId == request.ProcessId))
            {
                throw new IdentityCommandException(400, RiskRules.Invalid);
            }
            await RequireNoBlockingTreatmentsAsync(transaction, id, includeDrafts: true);
            var processes = await ReadActiveProcessesAsync([request.ProcessId], transaction);
            var changes = new List<(string Path, object? Before, object After)>();
            foreach (var link in links)
            {
                var closed = await SaveProcessLinkAsync(transaction, link, link.IsPrimary, false);
                changes.Add((BusinessHistory.ProcessesPath, link, closed));
            }

            var added = await InsertProcessLinkAsync(transaction, id, processes[0], true);
            changes.Add((BusinessHistory.ProcessesPath, null, added));
            return await StartSingleReviewAsync(risk, ReviewRules.Manual, ReviewRules.Transfer, id,
                actor.UserId, Guid.NewGuid(), transaction, changes);
        });
    }

    /// <inheritdoc/>
    public async Task<RiskReviewView[]> ListRiskReviewsAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        await ReadAsync<RiskDal>(id, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
        var history = await ReadRiskReviewsAsync(id, offset, limit, transaction);
        transaction.Commit();
        return history;
    }
    /// <summary>Читает хронологию без секретов и исходных тел вебхука</summary>
    /// <param name="id">Риск</param>
    /// <param name="offset">Смещение окна</param>
    /// <param name="limit">Размер окна</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <returns>История результатов со снимками</returns>
    private async Task<RiskReviewView[]> ReadRiskReviewsAsync(Guid id, int offset, int limit, IDbTransaction transaction)
    {
        var waiting = ReviewRules.Waiting;
        var query = $"""
            SELECT rr.*, run.{EntityMapper.ColName<ReviewRunDal>(x => x.Id)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.Reason)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.SourceType)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.SourceId)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.SourceNameSnapshot)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.ExternalSystemNameSnapshot)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.InitiatedById)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.ConfigurationAuthorId)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.ResponsibleUserId)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.TimeZone)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.ScheduledDate)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.OccurredAt)}, run.{EntityMapper.ColName<ReviewRunDal>(x => x.Status)}
            FROM {EntityMapper.TbName<RiskReviewDal>()} rr JOIN {EntityMapper.TbName<ReviewRunDal>()} run ON run.{EntityMapper.ColName<ReviewRunDal>(x => x.Id)} = rr.{EntityMapper.ColName<RiskReviewDal>(x => x.ReviewRunId)}
            WHERE rr.{EntityMapper.ColName<RiskReviewDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(id))}
            ORDER BY (rr.{EntityMapper.ColName<RiskReviewDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(waiting))}) DESC, rr.{EntityMapper.ColName<RiskReviewDal>(x => x.ProcessedAt)} DESC, rr.{EntityMapper.ColName<RiskReviewDal>(x => x.Id)} DESC
            OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var rows = await connection.QueryAsync<RiskReviewDal, ReviewRunDal, RiskReviewView>(query,
            (review, run) => new RiskReviewView(review, run), new { id, offset, limit, waiting }, transaction,
            splitOn: EntityMapper.ColName<ReviewRunDal>(x => x.Id, false));
        return rows.ToArray();
    }
}
