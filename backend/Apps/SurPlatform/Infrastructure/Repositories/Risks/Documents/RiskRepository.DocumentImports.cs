using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.UseCases.Documents.Dto.Request;
using Application.UseCases.Documents.Dto.Response;
using Application.UseCases.Risks.Dto;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Microsoft.VisualBasic.FileIO;

namespace Infrastructure.Repositories.Risks;

/// <summary>Разбирает CSV и фиксирует каждую строку с результатом регистрации риска</summary>
internal sealed partial class RiskRepository
{
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
        await documentStorage.ReadAsync(DocumentKey(job.Id, checksum), async (stream, token) =>
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
            if (await connection.QuerySingleOrDefaultAsync<bool?>(existingQuery, new { id, number }, transaction) is bool existing)
            {
                return existing;
            }

            var savepoint = EntityMapper.Identifier(nameof(ImportRowAsync));
            var saveQuery = $"SAVEPOINT {savepoint}";
            await connection.ExecuteAsync(saveQuery, transaction: transaction);
            string? code = null;
            Guid? riskId = null;
            try
            {
                var request = MapImportRow(mapping, fields);
                var risk = await CreateRiskAsync(actor, request, transaction);
                riskId = risk.Id;
            }
            catch (Exception error) when (error is FormatException or ValidationException or IdentityCommandException ||
                error is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.CharacterNotInRepertoire })
            {
                var rollbackQuery = $"ROLLBACK TO SAVEPOINT {savepoint}";
                await connection.ExecuteAsync(rollbackQuery, transaction: transaction);
                code = error is IdentityCommandException command ? command.Code : DocumentRules.InvalidRow;
            }

            var row = new DocumentRowDal
            {
                JobId = id, Number = number, HasError = code is not null,
                Data = JsonSerializer.Serialize(new { riskId, code }, JsonSerializerOptions.Web)
            };
            var insertQuery = $"""
                INSERT INTO {EntityMapper.TbName<DocumentRowDal>()} ({EntityMapper.ColName<DocumentRowDal>(x => x.JobId)},
                    {EntityMapper.ColName<DocumentRowDal>(x => x.Number)}, {EntityMapper.ColName<DocumentRowDal>(x => x.Data)},
                    {EntityMapper.ColName<DocumentRowDal>(x => x.HasError)})
                VALUES ({EntityMapper.ParameterName(nameof(row.JobId))}, {EntityMapper.ParameterName(nameof(row.Number))},
                    CAST({EntityMapper.ParameterName(nameof(row.Data))} AS jsonb), {EntityMapper.ParameterName(nameof(row.HasError))})
                """;
            await connection.ExecuteAsync(insertQuery, row, transaction);
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
            HazardId = Guid.Parse(ReadImportField(mapping, fields, nameof(RiskPassportRequest.HazardId))),
            CategoryId = Guid.Parse(ReadImportField(mapping, fields, nameof(RiskPassportRequest.CategoryId))),
            ProcessId = Guid.Parse(ReadImportField(mapping, fields, nameof(RiskPassportRequest.ProcessId))),
            AssetId = string.IsNullOrWhiteSpace(asset) ? null : Guid.Parse(asset)
        };
        Validator.ValidateObject(request, new ValidationContext(request), true);
        return request;
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
            throw new FormatException();
        }

        return fields[index];
    }
}
