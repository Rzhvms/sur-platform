using Domain.DalModels.Base;

namespace Domain.DalModels.Organization;

/// <summary>
/// Объект учета, стоимость простоя которого используется при оценке риска
/// </summary>
public sealed record AssetDal : VersionedDalModelBase, IOrganizationRow
{
    /// <summary>
    /// Доступность объекта для новых связей с рисками
    /// </summary>
    public bool IsActive { get; init; }
    
    /// <summary>
    /// Время создания записи в UTC
    /// </summary>
    public DateTime CreatedAt { get; init; }
    
    /// <summary>
    /// Время последнего изменения записи в UTC
    /// </summary>
    public DateTime UpdatedAt { get; init; }
    
    /// <summary>
    /// Название объекта учета
    /// </summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>
    /// Описание объекта учета
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// Инвентарный номер объекта при его наличии
    /// </summary>
    public string? InventoryNumber { get; init; }
    
    /// <summary>
    /// Стоимость календарного часа простоя объекта в рублях
    /// </summary>
    public decimal HourlyDowntimeCost { get; init; }
}