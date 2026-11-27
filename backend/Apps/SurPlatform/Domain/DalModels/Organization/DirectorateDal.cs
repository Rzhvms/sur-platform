using Domain.DalModels.Base;

namespace Domain.DalModels.Organization;

/// <summary>
/// Дирекция организационной структуры клиента
/// </summary>
public sealed record DirectorateDal : VersionedDalModelBase, IOrganizationRow
{
    /// <summary>
    /// Доступность дирекции для новых организационных связей
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
    /// Название дирекции
    /// </summary>
    public string Name { get; init; } = string.Empty;
}