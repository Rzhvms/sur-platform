namespace Domain.DalModels.Documents;

/// <summary>
/// Сохраненная строка отчетного снимка или результат обработки строки CSV
/// </summary>
public sealed record DocumentRowDal
{
    /// <summary>
    /// Идентификатор задания, которому принадлежит строка
    /// </summary>
    public Guid JobId { get; init; }

    /// <summary>
    /// Номер строки в снимке или исходном CSV
    /// </summary>
    public long Number { get; init; }

    /// <summary>
    /// Данные снимка или результат обработки строки в JSON
    /// </summary>
    public string Data { get; init; } = string.Empty;

    /// <summary>
    /// Признак ошибки обработки строки импорта
    /// </summary>
    public bool HasError { get; init; }
}