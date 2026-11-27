using Application.Models;
using Application.UseCases.Documents.Dto.Request;
using Application.UseCases.Documents.Dto.Response;

namespace Application.Ports.Repositories;

/// <summary>Клиентские задания документов и подтверждение их результатов</summary>
public interface IDocumentRepository
{
    /// <summary>Регистрирует согласованный отчётный снимок</summary>
    /// <param name="actor">Участник запроса</param>
    /// <param name="request">Параметры команды</param>
    /// <returns>Подтверждённое состояние операции или запрошенные данные</returns>
    Task<DocumentStatus> RequestReportAsync(RiskActor actor, ReportRequest request);

    /// <summary>Регистрирует административную выгрузку журнала</summary>
    /// <param name="actor">Участник запроса</param>
    /// <param name="request">Параметры команды</param>
    /// <returns>Подтверждённое состояние операции или запрошенные данные</returns>
    Task<DocumentStatus> RequestHistoryAsync(RiskActor actor, HistoryDocumentRequest request);

    /// <summary>Регистрирует загрузку исходного CSV</summary>
    /// <param name="actor">Участник запроса</param>
    /// <param name="request">Параметры команды</param>
    /// <returns>Подтверждённое состояние операции или запрошенные данные</returns>
    Task<DocumentStatus> RequestImportAsync(RiskActor actor, ImportRequest request);

    /// <summary>Читает состояние своего задания</summary>
    /// <param name="actor">Участник запроса</param>
    /// <param name="id">Идентификатор задания или файла</param>
    /// <returns>Подтверждённое состояние операции или запрошенные данные</returns>
    Task<DocumentStatus> StatusAsync(RiskActor actor, Guid id);

    /// <summary>Фиксирует сопоставление и запускает импорт</summary>
    /// <param name="actor">Участник запроса</param>
    /// <param name="id">Идентификатор задания или файла</param>
    /// <param name="mapping">Сопоставление колонок</param>
    /// <returns>Подтверждённое состояние операции или запрошенные данные</returns>
    Task<DocumentStatus> StartImportAsync(RiskActor actor, Guid id, ImportMapping mapping);

    /// <summary>Возобновляет прерванную обработку</summary>
    /// <param name="actor">Участник запроса</param>
    /// <param name="id">Идентификатор задания или файла</param>
    /// <returns>Подтверждённое состояние операции или запрошенные данные</returns>
    Task<DocumentStatus> RetryAsync(RiskActor actor, Guid id);

    /// <summary>Читает порцию строк снимка или результатов импорта</summary>
    /// <param name="actor">Участник запроса</param>
    /// <param name="id">Идентификатор задания или файла</param>
    /// <param name="after">Последняя уже прочитанная строка</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Подтверждённое состояние операции или запрошенные данные</returns>
    Task<DocumentRow[]> RowsAsync(RiskActor actor, Guid id, long after, int limit);

    /// <summary>Подтверждает поток исходного CSV</summary>
    /// <param name="actor">Участник запроса</param>
    /// <param name="id">Идентификатор задания или файла</param>
    /// <param name="source">Исходный поток CSV</param>
    /// <param name="length">Заявленная длина потока</param>
    /// <param name="cancellation">Отмена запроса</param>
    /// <returns>Подтверждённое состояние операции или запрошенные данные</returns>
    Task<DocumentStatus> UploadAsync(RiskActor actor, Guid id, Stream source, long length, CancellationToken cancellation);

    /// <summary>Проверяет доступ и передаёт файл в течение соединения с хранилищем</summary>
    /// <param name="actor">Участник запроса</param>
    /// <param name="id">Идентификатор задания или файла</param>
    /// <param name="transfer">Приёмник подтверждённых заголовков и содержимого</param>
    /// <param name="cancellation">Отмена запроса</param>
    Task DownloadAsync(RiskActor actor, Guid id, Func<DocumentFile, Stream, CancellationToken, Task> transfer, CancellationToken cancellation);
}
