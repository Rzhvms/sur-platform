using System.Data;
using System.Security.Cryptography;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.UseCases.Documents.Dto.Request;
using Application.UseCases.Documents.Dto.Response;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Minio.Exceptions;

namespace Infrastructure.Repositories.Risks;

/// <summary>Подтверждение файлов и защищённая потоковая выдача</summary>
internal sealed partial class RiskRepository
{
    /// <summary>Изолированная область блокировок файловых операций</summary>
    private const string FileLockPrefix = "sur-documents:";

    /// <summary>Безопасное имя исходного CSV</summary>
    private const string SourceName = "risk-import.csv";

    /// <summary>Безопасное имя выгрузки реестра CSV</summary>
    private const string ReportCsvName = "risk-report.csv";

    /// <summary>Безопасное имя книги Excel</summary>
    private const string ReportExcelName = "risk-report.xlsx";

    /// <summary>Безопасное имя административной выгрузки</summary>
    private const string HistoryName = "business-history.json";

    /// <inheritdoc/>
    public async Task<DocumentStatus> UploadAsync(RiskActor actor, Guid id, Stream source, long length, CancellationToken cancellation)
    {
        var job = await WriteAsync(actor, null, transaction => ReadOwnedDocumentAsync(actor, id, transaction));
        if (job.Kind != DocumentRules.Import || job.Size != length || job.Checksum is not { } checksum)
        {
            throw new IdentityCommandException(409, DocumentRules.Conflict);
        }

        if (job.State != DocumentRules.Upload)
        {
            return await StatusAsync(actor, id);
        }

        if (!await TryLockDocumentAsync(id))
        {
            throw new IdentityCommandException(409, DocumentRules.Conflict);
        }

        try
        {
            job = await WriteAsync(actor, null, transaction => ReadOwnedDocumentAsync(actor, id, transaction));
            if (job.State != DocumentRules.Upload)
            {
                return await DocumentStatusAsync(job);
            }

            var key = DocumentKey(id, checksum);
            if (!await documentStorage.ConfirmAsync(key, length, checksum, cancellation))
            {
                await using var buffer = OpenDocumentBuffer();
                await source.CopyToAsync(buffer, cancellation);
                buffer.Position = 0;
                var actual = Convert.ToHexString(await SHA256.HashDataAsync(buffer, cancellation));
                if (buffer.Length != length || !string.Equals(actual, checksum, StringComparison.Ordinal))
                {
                    throw new IdentityCommandException(400, DocumentRules.Invalid);
                }

                buffer.Position = 0;
                await documentStorage.SaveAsync(key, buffer, length, checksum, cancellation);
            }

            return await WriteAsync(actor, null, async transaction =>
            {
                var current = await ReadOwnedDocumentAsync(actor, id, transaction);
                var ready = current with { State = DocumentRules.Mapping };
                await SetDocumentStateAsync(ready, transaction);
                return await DocumentStatusAsync(ready, transaction);
            });
        }
        catch (MinioException) when (!cancellation.IsCancellationRequested)
        {
            throw new IdentityCommandException(503, DocumentRules.Unavailable);
        }
        finally
        {
            await UnlockDocumentAsync(id);
        }
    }

    /// <inheritdoc/>
    public async Task DownloadAsync(RiskActor actor, Guid id, Func<DocumentFile, Stream, CancellationToken, Task> transfer,
        CancellationToken cancellation)
    {
        DocumentJobDal job;
        using (var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead))
        {
            job = await ReadOwnedDocumentAsync(actor, id, transaction);
            transaction.Commit();
        }

        var ready = job.Kind == DocumentRules.Import ? job.State != DocumentRules.Upload : job.State == DocumentRules.Completed;
        if (!ready || job.Size is not long size || job.Checksum is not { } checksum)
        {
            throw new IdentityCommandException(404, DocumentRules.Missing);
        }

        var (name, contentType) = job.Kind switch
        {
            DocumentRules.Import => (SourceName, DocumentRules.CsvMediaType),
            DocumentRules.History => (HistoryName, DocumentRules.JsonMediaType),
            _ when ReadParameters<ReportRequest>(job).Format == DocumentRules.Xlsx => (ReportExcelName, DocumentRules.ExcelMediaType),
            _ => (ReportCsvName, DocumentRules.CsvMediaType)
        };
        try
        {
            await documentStorage.ReadAsync(DocumentKey(id, checksum),
                (stream, token) => transfer(new DocumentFile(name, contentType, size, checksum), stream, token), cancellation);
        }
        catch (MinioException) when (!cancellation.IsCancellationRequested)
        {
            throw new IdentityCommandException(503, DocumentRules.Unavailable);
        }
    }

    /// <summary>Создаёт ключ из канонического хоста клиента и серверного идентификатора файла</summary>
    /// <param name="id">Случайный локальный идентификатор файла</param>
    /// <param name="checksum">Подтверждённый SHA-256 неизменяемого содержимого</param>
    /// <returns>Ключ объекта, изолированный от других клиентов</returns>
    private string DocumentKey(Guid id, string checksum)
    {
        return $"{databaseContext.Client.Host}/{id:N}/{checksum}";
    }

    /// <summary>Использует удаляемый временный файл для повторного чтения SDK и проверки SHA-256</summary>
    /// <returns>Поток с автоматическим удалением при закрытии</returns>
    private static FileStream OpenDocumentBuffer()
    {
        return new FileStream(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), FileMode.CreateNew,
            FileAccess.ReadWrite, FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
    }

    /// <summary>Исключает параллельную обработку без удержания транзакции на внешнем I/O</summary>
    /// <param name="id">Задание клиента</param>
    /// <returns>Блокировка получена текущим соединением</returns>
    private Task<bool> TryLockDocumentAsync(Guid id)
    {
        var name = FileLockPrefix + id;
        var query = $"SELECT pg_try_advisory_lock(hashtextextended({EntityMapper.ParameterName(nameof(name))}, 0))";
        return connection.ExecuteScalarAsync<bool>(query, new { name });
    }

    /// <summary>Освобождает блокировку задания после обработки</summary>
    /// <param name="id">Задание клиента</param>
    private Task UnlockDocumentAsync(Guid id)
    {
        var name = FileLockPrefix + id;
        var query = $"SELECT pg_advisory_unlock(hashtextextended({EntityMapper.ParameterName(nameof(name))}, 0))";
        return connection.ExecuteAsync(query, new { name });
    }
}
