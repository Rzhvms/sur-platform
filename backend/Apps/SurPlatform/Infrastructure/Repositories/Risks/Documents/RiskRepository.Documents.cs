using System.Data;
using System.Text.Json;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.UseCases.Documents.Dto.Request;
using Application.UseCases.Documents.Dto.Response;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;

namespace Infrastructure.Repositories.Risks;

/// <summary>Авторизация и жизненный цикл клиентских файловых заданий</summary>
internal sealed partial class RiskRepository
{
    /// <inheritdoc/>
    public async Task<DocumentStatus> StatusAsync(RiskActor actor, Guid id)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
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

        _ = Boundaries(request.Period);
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
            var existing = await connection.QuerySingleOrDefaultAsync<DocumentJobDal>(findQuery, new { ownerId, kind, operationId }, transaction);
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
            await connection.ExecuteAsync(insertQuery, row, transaction);
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
        return connection.QuerySingleOrDefaultAsync<DocumentJobDal>(query, new { id }, transaction);
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
            (processed, errors) = await connection.QuerySingleAsync<(long, long)>(query, new { id }, transaction);
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
        return connection.ExecuteAsync(query, job, transaction);
    }

    /// <inheritdoc/>
    public async Task<DocumentRow[]> RowsAsync(RiskActor actor, Guid id, long after, int limit)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
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
        return connection.QueryAsync<DocumentRowDal>(query, new { id, after, limit }, transaction);
    }
}
