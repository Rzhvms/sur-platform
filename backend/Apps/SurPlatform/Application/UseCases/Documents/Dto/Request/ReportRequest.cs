namespace Application.UseCases.Documents.Dto.Request;

/// <summary>Параметры выборки реестра, оценок и мер</summary>
public sealed record ReportRequest
{
    /// <summary>Устойчивый ключ операции</summary>
    public Guid OperationId { get; init; }

    /// <summary>Момент исторического среза с часовым смещением</summary>
    public DateTimeOffset? AsOf { get; init; }

    /// <summary>Фильтр по действовавшей связи с процессом</summary>
    public Guid? ProcessId { get; init; }

    /// <summary>Фильтр по дирекции процесса</summary>
    public Guid? DirectorateId { get; init; }

    /// <summary>Фильтр по категории риска</summary>
    public Guid? CategoryId { get; init; }

    /// <summary>Фильтр по статусу риска</summary>
    public string? Status { get; init; }

    /// <summary>CSV или XLSX, пустое значение для просмотра снимка</summary>
    public string? Format { get; init; }
}
