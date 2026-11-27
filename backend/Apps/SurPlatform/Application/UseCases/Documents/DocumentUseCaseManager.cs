using Application.Models;
using Application.UseCases.Documents.Dto.Request;
using Application.UseCases.Documents.Dto.Response;
using Application.Ports.Repositories;

namespace Application.UseCases.Documents;

/// <summary>Прикладные команды отчётности и импорта</summary>
/// <param name="documents">Клиентские задания</param>
public sealed class DocumentUseCaseManager(IDocumentRepository documents)
{
    /// <summary>Регистрирует согласованный отчётный снимок</summary>
    /// <param name="actor">Участник запроса</param>
    /// <param name="request">Параметры команды</param>
    /// <returns>Подтверждённое состояние операции или запрошенные данные</returns>
    public Task<DocumentStatus> RequestReportAsync(RiskActor actor, ReportRequest request)
    {
        return documents.RequestReportAsync(actor, request);
    }

    /// <summary>Регистрирует административную выгрузку журнала</summary>
    /// <param name="actor">Участник запроса</param>
    /// <param name="request">Параметры команды</param>
    /// <returns>Подтверждённое состояние операции или запрошенные данные</returns>
    public Task<DocumentStatus> RequestHistoryAsync(RiskActor actor, HistoryDocumentRequest request)
    {
        return documents.RequestHistoryAsync(actor, request);
    }

    /// <summary>Регистрирует загрузку исходного CSV</summary>
    /// <param name="actor">Участник запроса</param>
    /// <param name="request">Параметры команды</param>
    /// <returns>Подтверждённое состояние операции или запрошенные данные</returns>
    public Task<DocumentStatus> RequestImportAsync(RiskActor actor, ImportRequest request)
    {
        return documents.RequestImportAsync(actor, request);
    }

    /// <summary>Читает состояние своего задания</summary>
    /// <param name="actor">Участник запроса</param>
    /// <param name="id">Идентификатор задания или файла</param>
    /// <returns>Подтверждённое состояние операции или запрошенные данные</returns>
    public Task<DocumentStatus> StatusAsync(RiskActor actor, Guid id)
    {
        return documents.StatusAsync(actor, id);
    }

    /// <summary>Фиксирует сопоставление и запускает импорт</summary>
    /// <param name="actor">Участник запроса</param>
    /// <param name="id">Идентификатор задания или файла</param>
    /// <param name="mapping">Сопоставление колонок</param>
    /// <returns>Подтверждённое состояние операции или запрошенные данные</returns>
    public Task<DocumentStatus> StartImportAsync(RiskActor actor, Guid id, ImportMapping mapping)
    {
        return documents.StartImportAsync(actor, id, mapping);
    }

    /// <summary>Возобновляет прерванную обработку</summary>
    /// <param name="actor">Участник запроса</param>
    /// <param name="id">Идентификатор задания или файла</param>
    /// <returns>Подтверждённое состояние операции или запрошенные данные</returns>
    public Task<DocumentStatus> RetryAsync(RiskActor actor, Guid id)
    {
        return documents.RetryAsync(actor, id);
    }

    /// <summary>Читает порцию строк снимка или результатов импорта</summary>
    /// <param name="actor">Участник запроса</param>
    /// <param name="id">Идентификатор задания или файла</param>
    /// <param name="after">Последняя уже прочитанная строка</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Подтверждённое состояние операции или запрошенные данные</returns>
    public Task<DocumentRow[]> RowsAsync(RiskActor actor, Guid id, long after, int limit)
    {
        return documents.RowsAsync(actor, id, after, limit);
    }

    /// <summary>Подтверждает поток исходного CSV</summary>
    /// <param name="actor">Участник запроса</param>
    /// <param name="id">Идентификатор задания или файла</param>
    /// <param name="source">Исходный поток CSV</param>
    /// <param name="length">Заявленная длина потока</param>
    /// <param name="cancellation">Отмена запроса</param>
    /// <returns>Подтверждённое состояние операции или запрошенные данные</returns>
    public Task<DocumentStatus> UploadAsync(RiskActor actor, Guid id, Stream source, long length, CancellationToken cancellation)
    {
        return documents.UploadAsync(actor, id, source, length, cancellation);
    }

    /// <summary>Проверяет доступ и передаёт файл в течение соединения с хранилищем</summary>
    /// <param name="actor">Участник запроса</param>
    /// <param name="id">Идентификатор задания или файла</param>
    /// <param name="transfer">Приёмник подтверждённых заголовков и содержимого</param>
    /// <param name="cancellation">Отмена запроса</param>
    public Task DownloadAsync(RiskActor actor, Guid id, Func<DocumentFile, Stream, CancellationToken, Task> transfer, CancellationToken cancellation)
    {
        return documents.DownloadAsync(actor, id, transfer, cancellation);
    }
}
