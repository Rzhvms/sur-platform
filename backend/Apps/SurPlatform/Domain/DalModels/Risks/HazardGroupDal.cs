using Domain.DalModels.Base;

namespace Domain.DalModels.Risks;

/// <summary>
/// Группа для объединения связанных опасностей в справочнике
/// </summary>
public sealed record HazardGroupDal : VersionedDalModelBase
{
    /// <summary>
    /// Название группы опасностей
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Описание объединяемых группой опасностей
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Доступность группы для новых связей с опасностями
    /// </summary>
    public bool IsActive { get; init; }
}