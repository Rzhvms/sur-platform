namespace Domain.DalModels;

/// <summary>Материализованная строка отчёта или результат импорта</summary>
public sealed record DocumentRowDal
{
    /// <summary>Владелец строки</summary>
    public Guid JobId { get; init; }

    /// <summary>Порядковый номер в снимке или CSV</summary>
    public long Number { get; init; }

    /// <summary>Снимок строки или результат обработки в JSON</summary>
    public string Data { get; init; } = string.Empty;

    /// <summary>Строка импорта отклонена</summary>
    public bool HasError { get; init; }
}
