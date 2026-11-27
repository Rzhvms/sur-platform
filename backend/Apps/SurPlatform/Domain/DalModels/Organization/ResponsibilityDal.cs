using Domain.DalModels.Base;

namespace Domain.DalModels.Organization;

/// <summary>
/// Организационное назначение сотрудника в зоне ответственности
/// </summary>
public sealed record ResponsibilityDal : VersionedDalModelBase, IOrganizationRow
{
    /// <summary>
    /// Признак действующего организационного назначения
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
    /// Идентификатор сотрудника, которому назначена ответственность
    /// </summary>
    public Guid PersonId { get; init; }
    
    /// <summary>
    /// Идентификатор дирекции назначения, если ответственность ограничена дирекцией
    /// </summary>
    public Guid? DirectorateId { get; init; }
    
    /// <summary>
    /// Код вида организационной ответственности
    /// </summary>
    public string ResponsibilityType { get; init; } = string.Empty;
}