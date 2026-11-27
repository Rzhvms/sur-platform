using System.Net;
using Application.Constants;
using Application.Exceptions;
using Application.UseCases.Documents.Interfaces;
using Application.Models;
using Application.UseCases.Documents.Dto.Request;
using Application.UseCases.Documents.Dto.Response;
using Application.Ports.Repositories;

namespace Application.UseCases.Documents;

/// <summary>Прикладные команды отчётности и импорта</summary>
/// <param name="documents">Клиентские задания</param>
internal sealed class DocumentUseCaseManager(IDocumentRepository documents) : IDocumentUseCaseManager
{
    /// <inheritdoc/>
    public Task<DocumentStatus> RequestReportAsync(RiskActor actor, ReportRequest request)
    {
        return documents.RequestReportAsync(actor, request);
    }

    /// <inheritdoc/>
    public Task<DocumentStatus> RequestHistoryAsync(RiskActor actor, HistoryDocumentRequest request)
    {
        return documents.RequestHistoryAsync(actor, request);
    }

    /// <inheritdoc/>
    public Task<DocumentStatus> RequestImportAsync(RiskActor actor, ImportRequest request)
    {
        return documents.RequestImportAsync(actor, request);
    }

    /// <inheritdoc/>
    public Task<DocumentStatus> StatusAsync(RiskActor actor, Guid id)
    {
        return documents.StatusAsync(actor, id);
    }

    /// <inheritdoc/>
    public Task<DocumentStatus> StartImportAsync(RiskActor actor, Guid id, ImportMapping mapping)
    {
        return documents.StartImportAsync(actor, id, mapping);
    }

    /// <inheritdoc/>
    public Task<DocumentStatus> RetryAsync(RiskActor actor, Guid id)
    {
        return documents.RetryAsync(actor, id);
    }

    /// <inheritdoc/>
    public Task<DocumentRow[]> RowsAsync(RiskActor actor, Guid id, long after, int limit)
    {
        return documents.RowsAsync(actor, id, after, limit);
    }

    /// <inheritdoc/>
    public Task<DocumentStatus> UploadAsync(RiskActor actor, Guid id, Stream source, long length, CancellationToken cancellation)
    {
        if (length is <= 0 or > DocumentRules.MaxImportBytes)
        {
            throw new IdentityCommandException((int)HttpStatusCode.BadRequest, DocumentRules.Invalid);
        }

        return documents.UploadAsync(actor, id, source, length, cancellation);
    }

    /// <inheritdoc/>
    public Task DownloadAsync(RiskActor actor, Guid id, Func<DocumentFile, Stream, CancellationToken, Task> transfer, CancellationToken cancellation)
    {
        return documents.DownloadAsync(actor, id, transfer, cancellation);
    }
}
