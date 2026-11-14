using System.ComponentModel.DataAnnotations;
using Api.Extensions;
using Application.Constants;
using Application.Exceptions;
using Application.UseCases.Documents;
using Application.UseCases.Documents.Dto.Request;
using Application.UseCases.Documents.Dto.Response;
using Core.Api.Controllers.ControllerTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Public.Documents;

/// <summary>Отчёты, импорт реестра и защищённая выдача файлов текущего клиента</summary>
/// <param name="useCases">Прикладные операции документов</param>
[Authorize]
[Route(DocumentsRoute)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class DocumentsController(DocumentUseCaseManager useCases) : BasePublicController
{
    /// <summary>Общий сегмент маршрутов документов</summary>
    private const string DocumentsRoute = "documents";

    /// <summary>Создание снимка реестра</summary>
    private const string ReportsRoute = "reports";

    /// <summary>Регистрация загрузки CSV</summary>
    private const string ImportsRoute = "imports";

    /// <summary>Идентификатор сохранённого задания</summary>
    private const string JobRoute = "{id:guid}";

    /// <summary>Порция строк задания</summary>
    private const string RowsRoute = JobRoute + "/rows";

    /// <summary>Запуск импорта с сопоставлением</summary>
    private const string MappingRoute = JobRoute + "/mapping";

    /// <summary>Содержимое подтверждённого файла</summary>
    private const string FileRoute = JobRoute + "/file";

    /// <summary>Повтор задания после временной ошибки</summary>
    private const string RetryRoute = JobRoute + "/retry";

    /// <summary>Политика браузерных и промежуточных кешей личных файлов</summary>
    private const string NoStore = "private, no-store";

    /// <summary>HTTP-схема передачи файла в ответе</summary>
    private const string Attachment = "attachment";

    /// <summary>Заголовок проверенной контрольной суммы</summary>
    private const string ChecksumHeader = "X-Content-SHA256";

    /// <summary>Регистрирует неизменяемый запрос отчёта</summary>
    /// <param name="request">Ключ операции, фильтры и формат</param>
    /// <returns>Состояние задания</returns>
    [HttpPost(ReportsRoute)]
    [ProducesResponseType<DocumentStatus>(StatusCodes.Status200OK)]
    public Task<DocumentStatus> ReportAsync(ReportRequest request)
    {
        return useCases.RequestReportAsync(User.GetRiskActor(), request);
    }

    /// <summary>Регистрирует ожидаемое содержимое CSV</summary>
    /// <param name="request">Ключ операции, размер и SHA-256</param>
    /// <returns>Состояние загрузки</returns>
    [HttpPost(ImportsRoute)]
    [ProducesResponseType<DocumentStatus>(StatusCodes.Status200OK)]
    public Task<DocumentStatus> ImportAsync(ImportRequest request)
    {
        return useCases.RequestImportAsync(User.GetRiskActor(), request);
    }

    /// <summary>Читает текущее состояние задания</summary>
    /// <param name="id">Задание владельца</param>
    /// <returns>Состояние, ошибка и подтверждённый файл</returns>
    [HttpGet(JobRoute)]
    [ProducesResponseType<DocumentStatus>(StatusCodes.Status200OK)]
    public Task<DocumentStatus> StatusAsync(Guid id)
    {
        return useCases.StatusAsync(User.GetRiskActor(), id);
    }

    /// <summary>Читает строки отчёта или результаты импорта</summary>
    /// <param name="id">Задание владельца</param>
    /// <param name="after">Последний номер предыдущей порции</param>
    /// <param name="limit">Количество строк</param>
    /// <returns>Строки в порядке номеров</returns>
    [HttpGet(RowsRoute)]
    [ProducesResponseType<DocumentRow[]>(StatusCodes.Status200OK)]
    public Task<DocumentRow[]> RowsAsync(Guid id, [Range(0, DocumentRules.RowLimit)] long after = 0,
        [Range(1, DocumentRules.BatchSize)] int limit = DocumentRules.BatchSize)
    {
        return useCases.RowsAsync(User.GetRiskActor(), id, after, limit);
    }

    /// <summary>Фиксирует сопоставление и запускает импорт</summary>
    /// <param name="id">Задание владельца</param>
    /// <param name="mapping">Индексы полей CSV</param>
    /// <returns>Состояние задания</returns>
    [HttpPut(MappingRoute)]
    [ProducesResponseType<DocumentStatus>(StatusCodes.Status200OK)]
    public Task<DocumentStatus> StartAsync(Guid id, ImportMapping mapping)
    {
        return useCases.StartImportAsync(User.GetRiskActor(), id, mapping);
    }

    /// <summary>Возобновляет работу из сохранённых данных</summary>
    /// <param name="id">Задание владельца</param>
    /// <returns>Состояние после команды</returns>
    [HttpPost(RetryRoute)]
    [ProducesResponseType<DocumentStatus>(StatusCodes.Status200OK)]
    public Task<DocumentStatus> RetryAsync(Guid id)
    {
        return useCases.RetryAsync(User.GetRiskActor(), id);
    }

    /// <summary>Передаёт исходный CSV с зарегистрированными размером и контрольной суммой</summary>
    /// <param name="id">Задание владельца</param>
    /// <param name="cancellation">Отмена передачи</param>
    /// <returns>Подтверждённый источник</returns>
    /// <exception cref="IdentityCommandException">Заголовок длины отсутствует или размер недопустим</exception>
    [HttpPut(FileRoute)]
    [Consumes(DocumentRules.BinaryMediaType)]
    [RequestSizeLimit(DocumentRules.MaxImportBytes)]
    [ProducesResponseType<DocumentStatus>(StatusCodes.Status200OK)]
    public Task<DocumentStatus> UploadAsync(Guid id, CancellationToken cancellation)
    {
        if (Request.ContentLength is not long length || length is <= 0 or > DocumentRules.MaxImportBytes)
        {
            throw new IdentityCommandException(400, DocumentRules.Invalid);
        }

        return useCases.UploadAsync(User.GetRiskActor(), id, Request.Body, length, cancellation);
    }

    /// <summary>Проверяет текущий доступ и выдаёт подтверждённый файл вложением</summary>
    /// <param name="id">Серверный fileId</param>
    /// <param name="cancellation">Отмена передачи</param>
    [HttpGet(FileRoute)]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK, DocumentRules.BinaryMediaType)]
    public async Task DownloadAsync(Guid id, CancellationToken cancellation)
    {
        await useCases.DownloadAsync(User.GetRiskActor(), id, async (file, stream, token) =>
        {
            var disposition = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue(Attachment);
            disposition.SetHttpFileName(file.Name);
            Response.GetTypedHeaders().ContentDisposition = disposition;
            Response.Headers.CacheControl = NoStore;
            Response.Headers[ChecksumHeader] = file.Checksum;
            Response.ContentType = file.ContentType;
            Response.ContentLength = file.Size;
            await stream.CopyToAsync(Response.Body, token);
        }, cancellation);
    }
}
