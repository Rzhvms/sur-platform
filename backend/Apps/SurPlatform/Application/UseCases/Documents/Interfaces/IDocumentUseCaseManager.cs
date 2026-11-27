namespace Application.UseCases.Documents.Interfaces;

/// <summary>Прикладные команды отчётности и импорта</summary>
/// <param name="documents">Клиентские задания</param>
public interface IDocumentUseCaseManager
{
    /// <summary>Регистрирует согласованный отчётный снимок</summary>
    Task<DocumentStatus> RequestReportAsync(RiskActor actor, ReportRequest request);

    /// <summary>Регистрирует административную выгрузку журнала</summary>
    Task<DocumentStatus> RequestHistoryAsync(RiskActor actor, HistoryDocumentRequest request);

    /// <summary>Регистрирует загрузку исходного CSV</summary>
    Task<DocumentStatus> RequestImportAsync(RiskActor actor, ImportRequest request);

    /// <summary>Читает состояние своего задания</summary>
    Task<DocumentStatus> StatusAsync(RiskActor actor, Guid id);

    /// <summary>Фиксирует сопоставление и запускает импорт</summary>
    Task<DocumentStatus> StartImportAsync(RiskActor actor, Guid id, ImportMapping mapping);

    /// <summary>Возобновляет прерванную обработку</summary>
    Task<DocumentStatus> RetryAsync(RiskActor actor, Guid id);

    /// <summary>Читает порцию строк снимка или результатов импорта</summary>
    Task<DocumentRow[]> RowsAsync(RiskActor actor, Guid id, long after, int limit);

    /// <summary>Подтверждает поток исходного CSV</summary>
    Task<DocumentStatus> UploadAsync(RiskActor actor, Guid id, Stream source, long length);

    /// <summary>Проверяет доступ и передаёт файл в течение соединения с хранилищем</summary>
    public Task DownloadAsync(RiskActor actor, Guid id, Func<DocumentFile, Stream, CancellationToken, Task> transfer);
}