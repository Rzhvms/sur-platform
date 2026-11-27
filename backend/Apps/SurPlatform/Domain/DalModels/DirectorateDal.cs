namespace Domain.DalModels;

/// <summary>
/// Строка оргструктуры клиента
/// </summary>
public sealed record DirectorateDal : IOrganizationRow
{
    /// <summary>
    /// Идентификатор записи
    /// </summary>
    public Guid Id { get; init; }
    /// <summary>
    /// Версия для проверки конкурентного изменения
    /// </summary>
    public Guid Version { get; init; }
    /// <summary>
    /// Доступность для новых связей
    /// </summary>
    public bool IsActive { get; init; }
    /// <summary>
    /// Время создания UTC
    /// </summary>
    public DateTime CreatedAt { get; init; }
    /// <summary>
    /// Время последнего изменения UTC
    /// </summary>
    public DateTime UpdatedAt { get; init; }
    /// <summary>
    /// Название дирекции
    /// </summary>
    public string Name { get; init; } = string.Empty;
}
