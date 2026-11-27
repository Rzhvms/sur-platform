using Infrastructure.Repositories.History.Helpers;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.Ports.Repositories;
using Application.UseCases.Documents.Dto.Request;
using Application.UseCases.Documents.Dto.Response;
using Application.UseCases.Risks.Dto;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Clients;
using Infrastructure.Observability;
using Infrastructure.Repositories.Documents.Interfaces;
using Infrastructure.Repositories.History;
using Infrastructure.Repositories.Risks.Interfaces;
using Infrastructure.Repositories.Sessions.Interfaces;
using Infrastructure.Repositories.Shared;
using Infrastructure.Services.Documents;
using Microsoft.VisualBasic.FileIO;
using Minio.Exceptions;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;

namespace Infrastructure.Repositories.Documents;

/// <summary>Клиентские задания документов, импорта и файлов</summary>
internal sealed class DocumentRepository : ClientRepository, IDocumentRepository, IDocumentWorkerRepository
{
    /// <summary>Выбранный клиентский контекст</summary>
    private readonly ClientDatabaseContext _databaseContext;

    /// <summary>Изолированное файловое хранилище</summary>
    private readonly DocumentStorage _documentStorage;

    /// <summary>Паспорт, связи процессов и технический пересчет риска</summary>
    private readonly IRiskStateRepository _riskStateRepository;

    /// <summary>Инициализирует операции выбранной клиентской базы</summary>
    /// <param name="connection">Подключение выбранной клиентской базы</param>
    /// <param name="sessions">Проверка сессии и эффективных прав в транзакции</param>
    /// <param name="operations">Метрики операций базы</param>
    /// <param name="databaseContext">Выбранный клиентский контекст</param>
    /// <param name="documentStorage">Изолированное файловое хранилище</param>
    /// <param name="riskStateRepository">Паспорт, связи процессов и технический пересчет риска</param>
    public DocumentRepository(
        IDbConnection connection,
        ITransactionalSessionRepository sessions,
        OperationsMetrics operations,
        ClientDatabaseContext databaseContext,
        DocumentStorage documentStorage,
        IRiskStateRepository riskStateRepository)
        : base(connection, sessions, operations)
    {
        _databaseContext = databaseContext;
        _documentStorage = documentStorage;
        _riskStateRepository = riskStateRepository;
    }

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
            if (!await _documentStorage.ConfirmAsync(key, length, checksum, cancellation))
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
                await _documentStorage.SaveAsync(key, buffer, length, checksum, cancellation);
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
        using (var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead))
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
            await _documentStorage.ReadAsync(DocumentKey(id, checksum),
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
        return $"{_databaseContext.Client.Host}/{id:N}/{checksum}";
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
        return _connection.ExecuteScalarAsync<bool>(query, new { name });
    }

    /// <summary>Освобождает блокировку задания после обработки</summary>
    /// <param name="id">Задание клиента</param>
    private Task UnlockDocumentAsync(Guid id)
    {
        var name = FileLockPrefix + id;
        var query = $"SELECT pg_advisory_unlock(hashtextextended({EntityMapper.ParameterName(nameof(name))}, 0))";
        return _connection.ExecuteAsync(query, new { name });
    }

    /// <summary>Сохраняет проверенные метаданные перед загрузкой и завершает только подтверждённый файл</summary>
    /// <param name="job">Согласованный снимок</param>
    /// <param name="cancellation">Остановка формирования</param>
    private async Task GenerateDocumentAsync(DocumentJobDal job, CancellationToken cancellation)
    {
        if (job.Size is long size && job.Checksum is { } checksum &&
            await _documentStorage.ConfirmAsync(DocumentKey(job.Id, checksum), size, checksum, cancellation))
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
        await _documentStorage.SaveAsync(DocumentKey(job.Id, digest), buffer, buffer.Length, digest, cancellation);
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

    /// <summary>Поля, обязательные при регистрации риска</summary>
    private static readonly string[] RequiredColumns =
        [nameof(RiskPassportRequest.Name), nameof(RiskPassportRequest.Description),
         nameof(RiskPassportRequest.HazardId), nameof(RiskPassportRequest.CategoryId), nameof(RiskPassportRequest.ProcessId)];

    /// <inheritdoc/>
    public Task<DocumentStatus> StartImportAsync(RiskActor actor, Guid id, ImportMapping mapping)
    {
        if (mapping.Delimiter is not (DocumentRules.DefaultDelimiter or DocumentRules.SemicolonDelimiter) ||
            mapping.Columns is null || !RequiredColumns.All(mapping.Columns.ContainsKey) ||
            mapping.Columns.Any(column => (column.Value < 0 || column.Value >= DocumentRules.BatchSize) ||
                !RequiredColumns.Contains(column.Key) && column.Key != nameof(RiskPassportRequest.AssetId)) ||
            mapping.Columns.Values.Distinct().Count() != mapping.Columns.Count)
        {
            throw new IdentityCommandException(400, DocumentRules.Invalid);
        }

        return WriteAsync(actor, null, async transaction =>
        {
            var job = await ReadOwnedDocumentAsync(actor, id, transaction);
            if (job.Kind != DocumentRules.Import)
            {
                throw new IdentityCommandException(409, DocumentRules.Conflict);
            }

            await RequireRightAsync(actor, RiskRules.Create, transaction);
            var parameters = JsonSerializer.Serialize(mapping, JsonSerializerOptions.Web);
            if (job.Mapping is not null && JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(job.Mapping),
                JsonSerializer.Deserialize<JsonElement>(parameters)))
            {
                return await DocumentStatusAsync(job, transaction);
            }

            if (job.State != DocumentRules.Mapping)
            {
                throw new IdentityCommandException(409, DocumentRules.Conflict);
            }

            var started = job with
            {
                State = DocumentRules.Pending, Mapping = parameters, SessionId = actor.SessionId, AccessVersion = actor.AccessVersion
            };
            await SetDocumentStateAsync(started, transaction);
            return await DocumentStatusAsync(started, transaction);
        });
    }

    /// <summary>Перечитывает исходный CSV и продолжает только неподтверждённые строки</summary>
    /// <param name="actor">Инициатор с правом импорта и регистрации</param>
    /// <param name="job">Задание с неизменяемым сопоставлением</param>
    /// <param name="cancellation">Отмена фоновой обработки</param>
    private async Task ProcessImportAsync(RiskActor actor, DocumentJobDal job, CancellationToken cancellation)
    {
        var mapping = JsonSerializer.Deserialize<ImportMapping>(job.Mapping ?? throw new InvalidOperationException(), JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException();
        var status = await DocumentStatusAsync(job);
        long errors = status.Errors;
        await using var buffer = OpenDocumentBuffer();
        var checksum = job.Checksum ?? throw new InvalidOperationException();
        var size = job.Size ?? throw new InvalidOperationException();
        await _documentStorage.ReadAsync(DocumentKey(job.Id, checksum), async (stream, token) =>
        {
            await stream.CopyToAsync(buffer, token);
        }, cancellation);
        buffer.Position = 0;
        var digest = Convert.ToHexString(await SHA256.HashDataAsync(buffer, cancellation));
        if (buffer.Length != size || !string.Equals(digest, checksum, StringComparison.Ordinal))
        {
            throw new IdentityCommandException(503, DocumentRules.Unavailable);
        }

        buffer.Position = 0;
        var preamble = Encoding.UTF8.GetPreamble();
        var prefix = new byte[preamble.Length];
        if (await buffer.ReadAtLeastAsync(prefix, prefix.Length, false, cancellation) != preamble.Length ||
            !prefix.AsSpan().SequenceEqual(preamble))
        {
            buffer.Position = 0;
        }

        using var parser = new TextFieldParser(buffer, new UTF8Encoding(false, true), false, true)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(mapping.Delimiter);
        try
        {
            if (mapping.HasHeader && !parser.EndOfData)
            {
                _ = parser.ReadFields();
            }
            long number = 0;
            while (!parser.EndOfData)
            {
                cancellation.ThrowIfCancellationRequested();
                if (++number > DocumentRules.RowLimit)
                {
                    throw new IdentityCommandException(400, DocumentRules.InvalidCsv);
                }

                string[] fields;
                try
                {
                    fields = parser.ReadFields() ?? [];
                }
                catch (MalformedLineException)
                {
                    fields = [];
                }
                if (number <= status.Processed)
                {
                    continue;
                }

                if (await ImportRowAsync(actor, job.Id, number, mapping, fields))
                {
                    errors++;
                }
            }
        }
        catch (Exception error) when (error is MalformedLineException or DecoderFallbackException)
        {
            throw new IdentityCommandException(400, DocumentRules.InvalidCsv);
        }

        await SetDocumentStateAsync(job with
        {
            State = errors == 0 ? DocumentRules.Completed : DocumentRules.Partial, CompletedAt = DateTime.UtcNow
        });
    }

    /// <summary>Записывает риск или ошибку и уникальный результат строки одной транзакцией</summary>
    /// <param name="actor">Автор импорта</param>
    /// <param name="id">Задание</param>
    /// <param name="number">Номер записи данных</param>
    /// <param name="mapping">Сопоставление полей</param>
    /// <param name="fields">Поля CSV</param>
    /// <returns>Строка отклонена</returns>
    private Task<bool> ImportRowAsync(RiskActor actor, Guid id, long number, ImportMapping mapping, string[] fields)
    {
        return WriteAsync(actor, DocumentRules.ImportRight, async transaction =>
        {
            await RequireRightAsync(actor, RiskRules.Create, transaction);
            var existingQuery = $"""
                SELECT {EntityMapper.ColName<DocumentRowDal>(x => x.HasError)} FROM {EntityMapper.TbName<DocumentRowDal>()}
                WHERE {EntityMapper.ColName<DocumentRowDal>(x => x.JobId)} = {EntityMapper.ParameterName(nameof(id))}
                  AND {EntityMapper.ColName<DocumentRowDal>(x => x.Number)} = {EntityMapper.ParameterName(nameof(number))}
                """;
            if (await _connection.QuerySingleOrDefaultAsync<bool?>(existingQuery, new { id, number }, transaction) is bool existing)
            {
                return existing;
            }

            var savepoint = EntityMapper.Identifier(nameof(ImportRowAsync));
            var saveQuery = $"SAVEPOINT {savepoint}";
            await _connection.ExecuteAsync(saveQuery, transaction: transaction);
            string? code = null;
            string? field = null;
            Guid? riskId = null;
            try
            {
                var request = MapImportRow(mapping, fields);
                var risk = await _riskStateRepository.CreateRiskAsync(actor, request, transaction);
                riskId = risk.Id;
            }
            catch (Exception error) when (error is FormatException or ValidationException or IdentityCommandException ||
                error is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.CharacterNotInRepertoire })
            {
                var rollbackQuery = $"ROLLBACK TO SAVEPOINT {savepoint}";
                await _connection.ExecuteAsync(rollbackQuery, transaction: transaction);
                code = error is IdentityCommandException command ? command.Code : DocumentRules.InvalidRow;
                field = error switch
                {
                    FormatException => error.Message,
                    ValidationException validation => validation.ValidationResult.MemberNames.FirstOrDefault(),
                    _ => null
                };
            }

            var row = new DocumentRowDal
            {
                JobId = id, Number = number, HasError = code is not null,
                Data = JsonSerializer.Serialize(new { riskId, code, field }, JsonSerializerOptions.Web)
            };
            var insertQuery = $"""
                INSERT INTO {EntityMapper.TbName<DocumentRowDal>()} ({EntityMapper.ColName<DocumentRowDal>(x => x.JobId)},
                    {EntityMapper.ColName<DocumentRowDal>(x => x.Number)}, {EntityMapper.ColName<DocumentRowDal>(x => x.Data)},
                    {EntityMapper.ColName<DocumentRowDal>(x => x.HasError)})
                VALUES ({EntityMapper.ParameterName(nameof(row.JobId))}, {EntityMapper.ParameterName(nameof(row.Number))},
                    CAST({EntityMapper.ParameterName(nameof(row.Data))} AS jsonb), {EntityMapper.ParameterName(nameof(row.HasError))})
                """;
            await _connection.ExecuteAsync(insertQuery, row, transaction);
            return row.HasError;
        });
    }

    /// <summary>Преобразует CSV в обычный контракт создания риска</summary>
    /// <param name="mapping">Согласованные индексы</param>
    /// <param name="fields">Значения записи CSV</param>
    /// <returns>Проверенный паспорт</returns>
    /// <exception cref="FormatException">Отсутствует колонка или неверно задан идентификатор</exception>
    /// <exception cref="ValidationException">Паспорт не соответствует общим ограничениям</exception>
    private static RiskPassportRequest MapImportRow(ImportMapping mapping, string[] fields)
    {
        var asset = ReadImportField(mapping, fields, nameof(RiskPassportRequest.AssetId));
        var request = new RiskPassportRequest
        {
            Name = ReadImportField(mapping, fields, nameof(RiskPassportRequest.Name)),
            Description = ReadImportField(mapping, fields, nameof(RiskPassportRequest.Description)),
            HazardId = ReadIdentifier(mapping, fields, nameof(RiskPassportRequest.HazardId)),
            CategoryId = ReadIdentifier(mapping, fields, nameof(RiskPassportRequest.CategoryId)),
            ProcessId = ReadIdentifier(mapping, fields, nameof(RiskPassportRequest.ProcessId)),
            AssetId = string.IsNullOrWhiteSpace(asset) ? null : ReadIdentifier(mapping, fields, nameof(RiskPassportRequest.AssetId))
        };
        Validator.ValidateObject(request, new ValidationContext(request), true);
        return request;
    }

    /// <summary>Читает обязательный идентификатор справочника</summary>
    /// <param name="mapping">Индексы полей</param>
    /// <param name="fields">Ячейки строки</param>
    /// <param name="name">Поле целевого паспорта</param>
    /// <returns>Разобранный идентификатор</returns>
    /// <exception cref="FormatException">Значение не является идентификатором</exception>
    private static Guid ReadIdentifier(ImportMapping mapping, string[] fields, string name)
    {
        var value = ReadImportField(mapping, fields, name);
        if (!Guid.TryParse(value, out var id))
        {
            throw new FormatException(name);
        }

        return id;
    }

    /// <summary>Возвращает ячейку выбранной колонки</summary>
    /// <param name="mapping">Индексы полей</param>
    /// <param name="fields">Ячейки строки</param>
    /// <param name="name">Поле целевого паспорта</param>
    /// <returns>Значение или пустая строка для несопоставленного поля</returns>
    /// <exception cref="FormatException">В строке отсутствует выбранная колонка</exception>
    private static string ReadImportField(ImportMapping mapping, string[] fields, string name)
    {
        if (!mapping.Columns.TryGetValue(name, out var index))
        {
            return string.Empty;
        }

        if (index >= fields.Length || fields[index].Contains('\0'))
        {
            throw new FormatException(name);
        }

        return fields[index];
    }

    /// <summary>Копирует ожидающие и обработанные события административной истории</summary>
    /// <param name="job">Сохранённый запрос журнала</param>
    /// <param name="transaction">Согласованный снимок источника</param>
    private async Task CaptureHistoryDocumentAsync(DocumentJobDal job, IDbTransaction transaction)
    {
        var (from, until) = BusinessHistoryQuery.Boundaries(ReadParameters<BusinessPeriod>(job));
        var id = job.Id;
        var query = $"""
            INSERT INTO {EntityMapper.TbName<DocumentRowDal>()} ({EntityMapper.ColName<DocumentRowDal>(x => x.JobId)},
                {EntityMapper.ColName<DocumentRowDal>(x => x.Number)}, {EntityMapper.ColName<DocumentRowDal>(x => x.Data)},
                {EntityMapper.ColName<DocumentRowDal>(x => x.HasError)})
            SELECT {EntityMapper.ParameterName(nameof(id))}, row_number() OVER (
                ORDER BY {EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)},
                         {EntityMapper.ColName<BusinessChangeDal>(x => x.EventId)}),
                {EntityMapper.ColName<BusinessChangeDal>(x => x.Payload)}, false
            FROM ({BusinessHistoryQuery.JournalSource}) events
            WHERE {EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)} >= {EntityMapper.ParameterName(nameof(from))}
              AND {EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)} < {EntityMapper.ParameterName(nameof(until))}
            ORDER BY {EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)},
                     {EntityMapper.ColName<BusinessChangeDal>(x => x.EventId)}
            LIMIT {DocumentRules.RowLimit + 1}
            """;
        if (await _connection.ExecuteAsync(query, new { id, from, until }, transaction) > DocumentRules.RowLimit)
        {
            throw new IdentityCommandException(400, DocumentRules.Invalid);
        }
    }

    /// <summary>Записывает риски, оценки и меры вместе с историческими названиями</summary>
    /// <param name="job">Сохранённые фильтры отчёта</param>
    /// <param name="captured">Время согласованной выборки</param>
    /// <param name="transaction">Снимок версий и текущих таблиц</param>
    private async Task CaptureRiskReportAsync(DocumentJobDal job, DateTime captured, IDbTransaction transaction)
    {
        var filter = ReadParameters<ReportRequest>(job);
        var effectiveAt = filter.AsOf?.UtcDateTime ?? captured;
        var id = job.Id;
        var risks = ReportSource<RiskDal>(filter.AsOf.HasValue);
        var assessments = ReportSource<RiskAssessmentDal>(filter.AsOf.HasValue);
        var treatments = ReportSource<TreatmentDal>(filter.AsOf.HasValue);
        var links = ReportSource<RiskProcessDal>(filter.AsOf.HasValue);
        var processes = ReportSource<ProcessDal>(filter.AsOf.HasValue);
        var directorates = ReportSource<DirectorateDal>(filter.AsOf.HasValue);
        var hazards = ReportSource<HazardDal>(filter.AsOf.HasValue);
        var categories = ReportSource<RiskCategoryDal>(filter.AsOf.HasValue);
        var assets = ReportSource<AssetDal>(filter.AsOf.HasValue);
        var query = $"""
            WITH risks AS ({risks}), assessments AS ({assessments}), treatments AS ({treatments}),
                 links AS ({links}), processes AS ({processes}), directorates AS ({directorates}),
                 hazards AS ({hazards}), categories AS ({categories}), assets AS ({assets}),
            selected AS (
                SELECT r.* FROM risks r
                WHERE (CAST({EntityMapper.ParameterName(nameof(filter.Status))} AS text) IS NULL OR
                    r.{EntityMapper.ColName<RiskDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(filter.Status))})
                  AND (CAST({EntityMapper.ParameterName(nameof(filter.CategoryId))} AS uuid) IS NULL OR
                    r.{EntityMapper.ColName<RiskDal>(x => x.CategoryId)} = {EntityMapper.ParameterName(nameof(filter.CategoryId))})
                  AND EXISTS (
                    SELECT FROM links l JOIN processes p ON p.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = l.{EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)}
                    WHERE l.{EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)} = r.{EntityMapper.ColName<RiskDal>(x => x.Id)}
                      AND l.{EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL
                      AND (CAST({EntityMapper.ParameterName(nameof(filter.ProcessId))} AS uuid) IS NULL OR
                        p.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(filter.ProcessId))})
                      AND (CAST({EntityMapper.ParameterName(nameof(filter.DirectorateId))} AS uuid) IS NULL OR
                        p.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)} = {EntityMapper.ParameterName(nameof(filter.DirectorateId))}))
            )
            INSERT INTO {EntityMapper.TbName<DocumentRowDal>()} ({EntityMapper.ColName<DocumentRowDal>(x => x.JobId)},
                {EntityMapper.ColName<DocumentRowDal>(x => x.Number)}, {EntityMapper.ColName<DocumentRowDal>(x => x.Data)},
                {EntityMapper.ColName<DocumentRowDal>(x => x.HasError)})
            SELECT {EntityMapper.ParameterName(nameof(id))}, row_number() OVER (ORDER BY r.{EntityMapper.ColName<RiskDal>(x => x.Id)}),
                jsonb_build_object('{nameof(RiskDal)}', to_jsonb(r),
                    '{nameof(HazardDal)}', h.{EntityMapper.ColName<HazardDal>(x => x.Name)},
                    '{nameof(RiskCategoryDal)}', c.{EntityMapper.ColName<RiskCategoryDal>(x => x.Name)},
                    '{nameof(AssetDal)}', asset.{EntityMapper.ColName<AssetDal>(x => x.Name)},
                    '{nameof(ProcessDal)}', COALESCE(org.items, '[]'::jsonb),
                    '{nameof(RiskAssessmentDal)}', COALESCE(a.items, '[]'::jsonb),
                    '{nameof(TreatmentDal)}', COALESCE(m.items, '[]'::jsonb)),
                false
            FROM selected r
            LEFT JOIN hazards h ON h.{EntityMapper.ColName<HazardDal>(x => x.Id)} = r.{EntityMapper.ColName<RiskDal>(x => x.HazardId)}
            LEFT JOIN categories c ON c.{EntityMapper.ColName<RiskCategoryDal>(x => x.Id)} = r.{EntityMapper.ColName<RiskDal>(x => x.CategoryId)}
            LEFT JOIN assets asset ON asset.{EntityMapper.ColName<AssetDal>(x => x.Id)} = r.{EntityMapper.ColName<RiskDal>(x => x.AssetId)}
            LEFT JOIN LATERAL (
                SELECT jsonb_agg(jsonb_build_object('{nameof(ProcessDal)}', p.{EntityMapper.ColName<ProcessDal>(x => x.Name)},
                    '{nameof(DirectorateDal)}', d.{EntityMapper.ColName<DirectorateDal>(x => x.Name)})
                    ORDER BY p.{EntityMapper.ColName<ProcessDal>(x => x.Id)}) AS items
                FROM links l JOIN processes p ON p.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = l.{EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)}
                LEFT JOIN directorates d ON d.{EntityMapper.ColName<DirectorateDal>(x => x.Id)} = p.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)}
                WHERE l.{EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)} = r.{EntityMapper.ColName<RiskDal>(x => x.Id)}
                  AND l.{EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL
            ) org ON true
            LEFT JOIN LATERAL (
                SELECT jsonb_agg(to_jsonb(x) ORDER BY x.{EntityMapper.ColName<RiskAssessmentDal>(x => x.Id)}) AS items
                FROM assessments x WHERE x.{EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskId)} = r.{EntityMapper.ColName<RiskDal>(x => x.Id)}
            ) a ON true
            LEFT JOIN LATERAL (
                SELECT jsonb_agg(to_jsonb(x) ORDER BY x.{EntityMapper.ColName<TreatmentDal>(x => x.Id)}) AS items
                FROM treatments x WHERE x.{EntityMapper.ColName<TreatmentDal>(x => x.RiskId)} = r.{EntityMapper.ColName<RiskDal>(x => x.Id)}
            ) m ON true
            LIMIT {DocumentRules.RowLimit + 1}
            """;
        if (await _connection.ExecuteAsync(query, new { id, AsOf = effectiveAt, filter.Status, filter.CategoryId, filter.ProcessId, filter.DirectorateId },
            transaction) > DocumentRules.RowLimit)
        {
            throw new IdentityCommandException(400, DocumentRules.Invalid);
        }
    }

    /// <summary>Преобразует сохранённые camelCase и PascalCase версии в тип исходной таблицы</summary>
    /// <typeparam name="T">Разрешённый источник отчёта</typeparam>
    /// <param name="historical">Использовать исторические версии на заданный момент</param>
    /// <returns>SQL-проекция источника</returns>
    private static string ReportSource<T>(bool historical)
    {
        var table = EntityMapper.TbName<T>();
        if (!historical)
        {
            var currentQuery = $"SELECT * FROM {table}";
            return currentQuery;
        }

        var asOf = nameof(ReportRequest.AsOf);
        var entityId = EntityMapper.ColName<OrgHistoryDal>(x => x.EntityId);
        var recorded = EntityMapper.ColName<OrgHistoryDal>(x => x.RecordedAt);
        var snapshot = EntityMapper.ColName<OrgHistoryDal>(x => x.Snapshot);
        var query = $"""
            SELECT source.* FROM (
                SELECT DISTINCT ON ({entityId}) {snapshot}
                FROM {EntityMapper.TbName<OrgHistoryDal>()}
                WHERE {EntityMapper.ColName<OrgHistoryDal>(x => x.EntityType)} = '{EntityMapper.TbName<T>(false)}'
                  AND {recorded} <= {EntityMapper.ParameterName(asOf)}
                ORDER BY {entityId}, {recorded} DESC, {EntityMapper.ColName<OrgHistoryDal>(x => x.Id)} DESC
            ) v
            CROSS JOIN LATERAL jsonb_populate_record(NULL::{table}, (
                SELECT jsonb_object_agg(columns.attname, pair.value)
                FROM jsonb_each(v.{snapshot}) pair
                JOIN pg_attribute columns ON columns.attrelid = '{table}'::regclass
                    AND columns.attnum > 0 AND NOT columns.attisdropped
                    AND lower(columns.attname) = lower(pair.key)
            )) source
            """;
        return query;
    }

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
        foreach (var id in await _connection.QueryAsync<Guid>(query, new { states }))
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
        _operations.Age(DocumentMetricOperation, job.CreatedAt);
        var actor = new RiskActor(job.OwnerId, job.SessionId, job.AccessVersion);
        try
        {
            using (var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead))
            {
                await RequireDocumentRightAsync(actor, job.Kind, HasReportFormat(job), transaction);
                job = job with { State = DocumentRules.Processing };
                if (job.Kind != DocumentRules.Import && job.CapturedAt is null)
                {
                    var capturedQuery = "SELECT transaction_timestamp()";
                    var captured = await _connection.QuerySingleAsync<DateTime>(capturedQuery, transaction: transaction);
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
                _operations.Outcome(DocumentMetricOperation, completed.State);
            }
        }
        catch (Exception error) when (!cancellation.IsCancellationRequested)
        {
            _operations.Outcome(DocumentMetricOperation, DocumentRules.Failed);
            job = await ReadDocumentAsync(id) ?? job;
            await SetDocumentStateAsync(job with
            {
                State = DocumentRules.Failed, CompletedAt = DateTime.UtcNow,
                Error = error is IdentityCommandException command ? command.Code : DocumentRules.Unavailable
            });
        }
        finally
        {
            _operations.Duration(DocumentMetricOperation, started);
        }
    }

    /// <inheritdoc/>
    public async Task<DocumentStatus> StatusAsync(RiskActor actor, Guid id)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        var job = await ReadOwnedDocumentAsync(actor, id, transaction);
        var status = await DocumentStatusAsync(job, transaction);
        transaction.Commit();
        return status;
    }

    /// <inheritdoc/>
    public Task<DocumentStatus> RequestReportAsync(RiskActor actor, ReportRequest request)
    {
        if (request.Format is not (null or DocumentRules.Csv or DocumentRules.Xlsx) ||
            request.Status is { } status && !RiskRules.BoardStatuses.Contains(status) ||
            request.AsOf > DateTimeOffset.UtcNow)
        {
            throw new IdentityCommandException(400, DocumentRules.Invalid);
        }

        return RegisterDocumentAsync(actor, request.OperationId, DocumentRules.Report, request,
            DocumentRules.Pending, request.Format is not null);
    }

    /// <inheritdoc/>
    public Task<DocumentStatus> RequestHistoryAsync(RiskActor actor, HistoryDocumentRequest request)
    {
        if (request.Period is null)
        {
            throw new IdentityCommandException(400, DocumentRules.Invalid);
        }

        _ = BusinessHistoryQuery.Boundaries(request.Period);
        return RegisterDocumentAsync(actor, request.OperationId, DocumentRules.History, request.Period, DocumentRules.Pending);
    }

    /// <inheritdoc/>
    public Task<DocumentStatus> RequestImportAsync(RiskActor actor, ImportRequest request)
    {
        if (request.Size is <= 0 or > DocumentRules.MaxImportBytes ||
            request.Checksum is not { Length: DocumentRules.DigestLength } || !request.Checksum.All(Uri.IsHexDigit))
        {
            throw new IdentityCommandException(400, DocumentRules.Invalid);
        }

        return RegisterDocumentAsync(actor, request.OperationId, DocumentRules.Import,
            request with { Checksum = request.Checksum.ToUpperInvariant() }, DocumentRules.Upload);
    }

    /// <summary>Регистрирует операцию один раз для владельца и назначения</summary>
    /// <typeparam name="T">Тип неизменяемых параметров</typeparam>
    /// <param name="actor">Инициатор команды</param>
    /// <param name="operationId">Ключ повтора</param>
    /// <param name="kind">Назначение файла</param>
    /// <param name="request">Согласованные параметры</param>
    /// <param name="state">Начальный этап обработки</param>
    /// <param name="export">Запрошен файл отчёта</param>
    /// <returns>Состояние исходной или новой операции</returns>
    private Task<DocumentStatus> RegisterDocumentAsync<T>(RiskActor actor, Guid operationId, string kind,
        T request, string state, bool export = false)
    {
        if (operationId == Guid.Empty)
        {
            throw new IdentityCommandException(400, DocumentRules.Invalid);
        }

        return WriteAsync(actor, null, async transaction =>
        {
            await RequireDocumentRightAsync(actor, kind, export, transaction);
            var ownerId = actor.UserId;
            var parameters = JsonSerializer.Serialize(request, JsonSerializerOptions.Web);
            var findQuery = $"""
                SELECT * FROM {EntityMapper.TbName<DocumentJobDal>()}
                WHERE {EntityMapper.ColName<DocumentJobDal>(x => x.OwnerId)} = {EntityMapper.ParameterName(nameof(ownerId))}
                  AND {EntityMapper.ColName<DocumentJobDal>(x => x.Kind)} = {EntityMapper.ParameterName(nameof(kind))}
                  AND {EntityMapper.ColName<DocumentJobDal>(x => x.OperationId)} = {EntityMapper.ParameterName(nameof(operationId))}
                """;
            var existing = await _connection.QuerySingleOrDefaultAsync<DocumentJobDal>(findQuery, new { ownerId, kind, operationId }, transaction);
            if (existing is not null)
            {
                if (!JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(existing.Parameters),
                    JsonSerializer.Deserialize<JsonElement>(parameters)))
                {
                    throw new IdentityCommandException(409, DocumentRules.Conflict);
                }

                return await DocumentStatusAsync(existing, transaction);
            }

            var row = new DocumentJobDal
            {
                Id = Guid.NewGuid(), OwnerId = ownerId, Kind = kind, OperationId = operationId,
                SessionId = actor.SessionId, AccessVersion = actor.AccessVersion, State = state, Parameters = parameters,
                CreatedAt = DateTime.UtcNow,
                Size = request is ImportRequest import ? import.Size : null,
                Checksum = request is ImportRequest source ? source.Checksum : null
            };
            var insertQuery = $"""
                INSERT INTO {EntityMapper.TbName<DocumentJobDal>()} (
                    {EntityMapper.ColName<DocumentJobDal>(x => x.Id)}, {EntityMapper.ColName<DocumentJobDal>(x => x.OwnerId)},
                    {EntityMapper.ColName<DocumentJobDal>(x => x.Kind)}, {EntityMapper.ColName<DocumentJobDal>(x => x.OperationId)},
                    {EntityMapper.ColName<DocumentJobDal>(x => x.SessionId)}, {EntityMapper.ColName<DocumentJobDal>(x => x.AccessVersion)},
                    {EntityMapper.ColName<DocumentJobDal>(x => x.State)}, {EntityMapper.ColName<DocumentJobDal>(x => x.Parameters)},
                    {EntityMapper.ColName<DocumentJobDal>(x => x.CreatedAt)}, {EntityMapper.ColName<DocumentJobDal>(x => x.Size)},
                    {EntityMapper.ColName<DocumentJobDal>(x => x.Checksum)})
                VALUES ({EntityMapper.ParameterName(nameof(row.Id))}, {EntityMapper.ParameterName(nameof(row.OwnerId))},
                    {EntityMapper.ParameterName(nameof(row.Kind))}, {EntityMapper.ParameterName(nameof(row.OperationId))},
                    {EntityMapper.ParameterName(nameof(row.SessionId))}, {EntityMapper.ParameterName(nameof(row.AccessVersion))},
                    {EntityMapper.ParameterName(nameof(row.State))}, CAST({EntityMapper.ParameterName(nameof(row.Parameters))} AS jsonb),
                    {EntityMapper.ParameterName(nameof(row.CreatedAt))}, {EntityMapper.ParameterName(nameof(row.Size))},
                    {EntityMapper.ParameterName(nameof(row.Checksum))})
                """;
            await _connection.ExecuteAsync(insertQuery, row, transaction);
            return await DocumentStatusAsync(row, transaction);
        });
    }

    /// <summary>Проверяет владение и право для назначения задания</summary>
    /// <param name="actor">Действующий участник</param>
    /// <param name="id">Задание клиента</param>
    /// <param name="transaction">Транзакция проверки</param>
    /// <returns>Авторизованное задание</returns>
    private async Task<DocumentJobDal> ReadOwnedDocumentAsync(RiskActor actor, Guid id, IDbTransaction transaction)
    {
        var job = await ReadDocumentAsync(id, transaction);
        if (job is null || job.OwnerId != actor.UserId)
        {
            throw new IdentityCommandException(404, DocumentRules.Missing);
        }

        await RequireDocumentRightAsync(actor, job.Kind, HasReportFormat(job), transaction);
        return job;
    }

    /// <summary>Проверяет текущее право согласно назначению файла</summary>
    /// <param name="actor">Участник операции</param>
    /// <param name="kind">Назначение файла</param>
    /// <param name="export">Запрошен файловый отчёт</param>
    /// <param name="transaction">Согласованное чтение прав</param>
    private async Task RequireDocumentRightAsync(RiskActor actor, string kind, bool export, IDbTransaction transaction)
    {
        if (kind == DocumentRules.History)
        {
            await RequireRightAsync(actor, IdentityRights.AdminCode, transaction);
            return;
        }

        if (kind == DocumentRules.Import)
        {
            await RequireRightAsync(actor, DocumentRules.ImportRight, transaction);
            return;
        }

        await RequireRightAsync(actor, DocumentRules.View, transaction);
        if (export)
        {
            await RequireRightAsync(actor, DocumentRules.Export, transaction);
        }
    }

    /// <summary>Определяет, был ли запрошен результат на скачивание</summary>
    /// <param name="job">Сохранённое задание</param>
    /// <returns>Есть формат файла отчёта</returns>
    private static bool HasReportFormat(DocumentJobDal job)
    {
        return job.Kind == DocumentRules.Report && ReadParameters<ReportRequest>(job).Format is not null;
    }

    /// <summary>Читает неизменяемые параметры из базы</summary>
    /// <typeparam name="T">Тип параметров соответствующего сценария</typeparam>
    /// <param name="job">Сохранённое задание</param>
    /// <returns>Параметры операции</returns>
    private static T ReadParameters<T>(DocumentJobDal job) where T : class
    {
        return JsonSerializer.Deserialize<T>(job.Parameters, JsonSerializerOptions.Web) ?? throw new InvalidOperationException();
    }

    /// <summary>Загружает локальное задание</summary>
    /// <param name="id">Идентификатор задания</param>
    /// <param name="transaction">Текущая транзакция при наличии</param>
    /// <returns>Найденное задание</returns>
    private Task<DocumentJobDal?> ReadDocumentAsync(Guid id, IDbTransaction? transaction = null)
    {
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<DocumentJobDal>()}
            WHERE {EntityMapper.ColName<DocumentJobDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))}
            """;
        return _connection.QuerySingleOrDefaultAsync<DocumentJobDal>(query, new { id }, transaction);
    }

    /// <summary>Отображает состояние и подтверждённые счётчики</summary>
    /// <param name="job">Клиентское задание</param>
    /// <param name="transaction">Снимок состояния</param>
    /// <returns>Контракт чтения задания</returns>
    private async Task<DocumentStatus> DocumentStatusAsync(DocumentJobDal job, IDbTransaction? transaction = null)
    {
        long processed = 0;
        long errors = 0;
        if (job.Kind == DocumentRules.Import)
        {
            var id = job.Id;
            var query = $"""
                SELECT count(*), count(*) FILTER (WHERE {EntityMapper.ColName<DocumentRowDal>(x => x.HasError)})
                FROM {EntityMapper.TbName<DocumentRowDal>()}
                WHERE {EntityMapper.ColName<DocumentRowDal>(x => x.JobId)} = {EntityMapper.ParameterName(nameof(id))}
                """;
            (processed, errors) = await _connection.QuerySingleAsync<(long, long)>(query, new { id }, transaction);
        }

        var asOf = job.Kind == DocumentRules.Report
            ? ReadParameters<ReportRequest>(job).AsOf?.UtcDateTime ?? job.CapturedAt
            : null;
        return new DocumentStatus(job.Id, job.Kind, job.State,
            job.Size.HasValue && job.State != DocumentRules.Upload &&
            (job.Kind == DocumentRules.Import || job.State == DocumentRules.Completed) ? job.Id : null,
            job.CreatedAt, job.OwnerId, asOf, job.CapturedAt,
            job.State == DocumentRules.Completed ? job.GeneratedAt : null,
            job.CompletedAt, job.Error, CanRetryDocument(job), processed, errors);
    }

    /// <summary>Определяет возможность возобновить обработку с прежним источником</summary>
    /// <param name="job">Остановленное задание</param>
    /// <returns>Сбой допускает повтор</returns>
    private static bool CanRetryDocument(DocumentJobDal job)
    {
        return job.State == DocumentRules.Failed && job.Error is DocumentRules.Unavailable or IdentityErrorCodes.AccessDenied;
    }

    /// <inheritdoc/>
    public Task<DocumentStatus> RetryAsync(RiskActor actor, Guid id)
    {
        return WriteAsync(actor, null, async transaction =>
        {
            var job = await ReadOwnedDocumentAsync(actor, id, transaction);
            if (CanRetryDocument(job))
            {
                await SetDocumentStateAsync(job with
                {
                    State = DocumentRules.Pending, Error = null,
                    CompletedAt = null,
                    SessionId = actor.SessionId, AccessVersion = actor.AccessVersion
                }, transaction);
                job = await ReadDocumentAsync(id, transaction) ?? throw new InvalidOperationException();
            }
            else if (job.State == DocumentRules.Failed)
            {
                throw new IdentityCommandException(409, DocumentRules.Conflict);
            }

            return await DocumentStatusAsync(job, transaction);
        });
    }

    /// <summary>Фиксирует переход без изменения ключа и параметров задания</summary>
    /// <param name="job">Новое состояние</param>
    /// <param name="transaction">Транзакция перехода</param>
    private Task SetDocumentStateAsync(DocumentJobDal job, IDbTransaction? transaction = null)
    {
        var query = $"""
            UPDATE {EntityMapper.TbName<DocumentJobDal>()}
            SET {EntityMapper.ColName<DocumentJobDal>(x => x.State)} = {EntityMapper.ParameterName(nameof(job.State))},
                {EntityMapper.ColName<DocumentJobDal>(x => x.Error)} = {EntityMapper.ParameterName(nameof(job.Error))},
                {EntityMapper.ColName<DocumentJobDal>(x => x.CapturedAt)} = {EntityMapper.ParameterName(nameof(job.CapturedAt))},
                {EntityMapper.ColName<DocumentJobDal>(x => x.GeneratedAt)} = {EntityMapper.ParameterName(nameof(job.GeneratedAt))},
                {EntityMapper.ColName<DocumentJobDal>(x => x.CompletedAt)} = {EntityMapper.ParameterName(nameof(job.CompletedAt))},
                {EntityMapper.ColName<DocumentJobDal>(x => x.Size)} = {EntityMapper.ParameterName(nameof(job.Size))},
                {EntityMapper.ColName<DocumentJobDal>(x => x.Checksum)} = {EntityMapper.ParameterName(nameof(job.Checksum))},
                {EntityMapper.ColName<DocumentJobDal>(x => x.SessionId)} = {EntityMapper.ParameterName(nameof(job.SessionId))},
                {EntityMapper.ColName<DocumentJobDal>(x => x.AccessVersion)} = {EntityMapper.ParameterName(nameof(job.AccessVersion))},
                {EntityMapper.ColName<DocumentJobDal>(x => x.Mapping)} = CAST({EntityMapper.ParameterName(nameof(job.Mapping))} AS jsonb)
            WHERE {EntityMapper.ColName<DocumentJobDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(job.Id))}
            """;
        return _connection.ExecuteAsync(query, job, transaction);
    }

    /// <inheritdoc/>
    public async Task<DocumentRow[]> RowsAsync(RiskActor actor, Guid id, long after, int limit)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        var job = await ReadOwnedDocumentAsync(actor, id, transaction);
        if (job.Kind != DocumentRules.Import && (job.CapturedAt is null || job.Kind == DocumentRules.History))
        {
            throw new IdentityCommandException(409, DocumentRules.Conflict);
        }

        var rows = await ReadDocumentRowsAsync(id, after, limit, transaction);
        transaction.Commit();
        return rows.Select(row => new DocumentRow(row.Number, JsonSerializer.Deserialize<JsonElement>(row.Data), row.HasError)).ToArray();
    }

    /// <summary>Читает ограниченную порцию по курсору</summary>
    /// <param name="id">Материализованный набор</param>
    /// <param name="after">Последний прочитанный номер</param>
    /// <param name="limit">Размер порции</param>
    /// <param name="transaction">Текущий снимок при наличии</param>
    /// <returns>Строки в устойчивом порядке</returns>
    private Task<IEnumerable<DocumentRowDal>> ReadDocumentRowsAsync(Guid id, long after, int limit, IDbTransaction? transaction = null)
    {
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<DocumentRowDal>()}
            WHERE {EntityMapper.ColName<DocumentRowDal>(x => x.JobId)} = {EntityMapper.ParameterName(nameof(id))}
              AND {EntityMapper.ColName<DocumentRowDal>(x => x.Number)} > {EntityMapper.ParameterName(nameof(after))}
            ORDER BY {EntityMapper.ColName<DocumentRowDal>(x => x.Number)} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        return _connection.QueryAsync<DocumentRowDal>(query, new { id, after, limit }, transaction);
    }
}
