using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Application.Constants;
using Application.UseCases.Documents.Dto.Request;
using Domain.DalModels;
using Infrastructure.Services.Documents;

namespace Infrastructure.Repositories.Risks;

/// <summary>Формирование файлов из неизменяемого снимка и восстановление результата по ключу задания</summary>
internal sealed partial class RiskRepository
{
    /// <summary>Сохраняет проверенные метаданные перед загрузкой и завершает только подтверждённый файл</summary>
    /// <param name="job">Согласованный снимок</param>
    /// <param name="cancellation">Остановка формирования</param>
    private async Task GenerateDocumentAsync(DocumentJobDal job, CancellationToken cancellation)
    {
        if (job.Size is long size && job.Checksum is { } checksum &&
            await documentStorage.ConfirmAsync(DocumentKey(job.Id, checksum), size, checksum, cancellation))
        {
            await SetDocumentStateAsync(job with { State = DocumentRules.Completed, CompletedAt = DateTime.UtcNow });
            return;
        }

        job = job with { GeneratedAt = DateTime.UtcNow };
        var request = job.Kind == DocumentRules.Report ? ReadParameters<ReportRequest>(job) : null;
        var format = request?.Format ?? DocumentRules.Json;
        var captured = job.CapturedAt ?? throw new InvalidOperationException();
        var asOf = request?.AsOf?.UtcDateTime ?? captured;
        await using var buffer = OpenDocumentBuffer();
        await DocumentFormatter.WriteAsync(buffer, format, job, asOf, StreamDocumentRowsAsync(job.Id, cancellation), cancellation);
        DocumentFormatter.VerifyOutputSize(buffer);
        buffer.Position = 0;
        var digest = Convert.ToHexString(await SHA256.HashDataAsync(buffer, cancellation));
        var prepared = job with { Size = buffer.Length, Checksum = digest };
        await SetDocumentStateAsync(prepared);
        buffer.Position = 0;
        await documentStorage.SaveAsync(DocumentKey(job.Id, digest), buffer, buffer.Length, digest, cancellation);
        await SetDocumentStateAsync(prepared with { State = DocumentRules.Completed, CompletedAt = DateTime.UtcNow });
    }

    /// <summary>Выдаёт строки неизменяемого снимка ограниченными порциями</summary>
    /// <param name="id">Идентификатор снимка</param>
    /// <param name="cancellation">Отмена чтения</param>
    /// <returns>Строки по возрастающему номеру</returns>
    private async IAsyncEnumerable<DocumentRowDal> StreamDocumentRowsAsync(Guid id,
        [EnumeratorCancellation] CancellationToken cancellation)
    {
        long after = 0;
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            var batch = await ReadDocumentRowsAsync(id, after, DocumentRules.BatchSize);
            var found = false;
            foreach (var row in batch)
            {
                found = true;
                after = row.Number;
                yield return row;
            }
            if (!found)
            {
                yield break;
            }
        }
    }
}
