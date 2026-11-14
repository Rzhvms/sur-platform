using System.Data;
using System.Diagnostics;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;

namespace Infrastructure.Repositories.Risks;

/// <summary>Продолжение подтверждённых файловых заданий после перезапуска</summary>
internal sealed partial class RiskRepository
{
    /// <summary>Классификатор фоновой обработки документов</summary>
    private const string DocumentMetricOperation = "documents";

    /// <summary>Количество заданий на один проход клиентской очереди</summary>
    private const int JobsPerPass = 4;

    /// <inheritdoc/>
    public async Task ProcessDocumentsAsync(CancellationToken cancellation)
    {
        var states = new[] { DocumentRules.Pending, DocumentRules.Processing };
        var query = $"""
            SELECT {EntityMapper.ColName<DocumentJobDal>(x => x.Id)}
            FROM {EntityMapper.TbName<DocumentJobDal>()}
            WHERE {EntityMapper.ColName<DocumentJobDal>(x => x.State)} = ANY({EntityMapper.ParameterName(nameof(states))})
            ORDER BY {EntityMapper.ColName<DocumentJobDal>(x => x.CreatedAt)}, {EntityMapper.ColName<DocumentJobDal>(x => x.Id)}
            LIMIT {JobsPerPass}
            """;
        foreach (var id in await connection.QueryAsync<Guid>(query, new { states }))
        {
            cancellation.ThrowIfCancellationRequested();
            if (!await TryLockDocumentAsync(id))
            {
                continue;
            }

            try
            {
                await ProcessDocumentAsync(id, cancellation);
            }
            finally
            {
                await UnlockDocumentAsync(id);
            }
        }
    }

    /// <summary>Исполняет ранее принятую команду в контексте исходного пользователя</summary>
    /// <param name="id">Задание</param>
    /// <param name="cancellation">Остановка обработки</param>
    private async Task ProcessDocumentAsync(Guid id, CancellationToken cancellation)
    {
        var job = await ReadDocumentAsync(id);
        if (job is null || job.State is not (DocumentRules.Pending or DocumentRules.Processing))
        {
            return;
        }

        var started = Stopwatch.GetTimestamp();
        operations.Age(DocumentMetricOperation, job.CreatedAt);
        var actor = new RiskActor(job.OwnerId, job.SessionId, job.AccessVersion);
        try
        {
            using (var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead))
            {
                await RequireDocumentRightAsync(actor, job.Kind, HasReportFormat(job), transaction);
                job = job with { State = DocumentRules.Processing };
                if (job.Kind != DocumentRules.Import && job.CapturedAt is null)
                {
                    var capturedQuery = "SELECT transaction_timestamp()";
                    var captured = await connection.QuerySingleAsync<DateTime>(capturedQuery, transaction: transaction);
                    if (job.Kind == DocumentRules.History)
                    {
                        await CaptureHistoryDocumentAsync(job, transaction);
                    }
                    else
                    {
                        await CaptureRiskReportAsync(job, captured, transaction);
                    }

                    job = job with { CapturedAt = captured };
                }

                await SetDocumentStateAsync(job, transaction);
                transaction.Commit();
            }

            if (job.Kind == DocumentRules.Import)
            {
                await ProcessImportAsync(actor, job, cancellation);
            }
            else if (job.Kind == DocumentRules.History || HasReportFormat(job))
            {
                await GenerateDocumentAsync(job, cancellation);
            }
            else
            {
                await SetDocumentStateAsync(job with { State = DocumentRules.Completed, CompletedAt = DateTime.UtcNow });
            }

            var completed = await ReadDocumentAsync(id);
            if (completed is not null)
            {
                operations.Outcome(DocumentMetricOperation, completed.State);
            }
        }
        catch (Exception error) when (!cancellation.IsCancellationRequested)
        {
            operations.Outcome(DocumentMetricOperation, DocumentRules.Failed);
            job = await ReadDocumentAsync(id) ?? job;
            await SetDocumentStateAsync(job with
            {
                State = DocumentRules.Failed, CompletedAt = DateTime.UtcNow,
                Error = error is IdentityCommandException command ? command.Code : DocumentRules.Unavailable
            });
        }
        finally
        {
            operations.Duration(DocumentMetricOperation, started);
        }
    }
}
