using Domain.DalModels.Base;

namespace Domain.DalModels.Organization;

/// <summary>
/// Бизнес-процесс с принадлежностью к дирекции и назначенным владельцем
/// </summary>
public sealed record ProcessDal : VersionedDalModelBase, IOrganizationRow
{
    /// <summary>
    /// Доступность процесса для новых связей с рисками
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
    /// Идентификатор дирекции, которой принадлежит процесс
    /// </summary>
    public Guid DirectorateId { get; init; }
    
    /// <summary>
    /// Идентификатор сотрудника, назначенного владельцем процесса
    /// </summary>
    public Guid OwnerId { get; init; }
    
    /// <summary>
    /// Название бизнес-процесса
    /// </summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>
    /// Описание бизнес-процесса
    /// </summary>
    public string? Description { get; init; }
}