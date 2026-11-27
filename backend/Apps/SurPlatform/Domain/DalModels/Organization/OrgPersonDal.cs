using Domain.DalModels.Base;

namespace Domain.DalModels.Organization;

/// <summary>
/// Запись сотрудника, связывающая пользователя с его положением в оргструктуре
/// </summary>
public sealed record OrgPersonDal : BaseUserModel, IOrganizationRow
{
    /// <summary>
    /// Признак действующей записи сотрудника
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
    /// Идентификатор дирекции сотрудника, если она назначена
    /// </summary>
    public Guid? DirectorateId { get; init; }
    
    /// <summary>
    /// Название должности сотрудника
    /// </summary>
    public string? PositionName { get; init; }
    
    /// <summary>
    /// ФИО из связанной учетной записи при чтении сотрудника
    /// </summary>
    public string? FullName { get; init; }
    
    /// <summary>
    /// Email из связанной учетной записи при чтении сотрудника
    /// </summary>
    public string? Email { get; init; }
}