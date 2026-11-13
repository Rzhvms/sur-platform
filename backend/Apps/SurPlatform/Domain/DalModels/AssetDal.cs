namespace Domain.DalModels;

/// <summary>
/// Строка оргструктуры клиента
/// </summary>
public sealed record AssetDal : IOrganizationRow
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
    /// Название объекта
    /// </summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>
    /// Описание объекта
    /// </summary>
    public string? Description { get; init; }
    /// <summary>
    /// Инвентарный номер
    /// </summary>
    public string? InventoryNumber { get; init; }
    /// <summary>
    /// Стоимость календарного часа простоя в рублях
    /// </summary>
    public decimal HourlyDowntimeCost { get; init; }
}
