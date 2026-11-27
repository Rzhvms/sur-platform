using Domain.DalModels.Base;

namespace Domain.DalModels.Organization;

/// <summary>
/// Исторический снимок атрибутов записи организационной структуры
/// </summary>
public sealed record OrgHistoryDal : DalModelBase
{
    /// <summary>
    /// Идентификатор записи, для которой сохранен снимок
    /// </summary>
    public Guid EntityId { get; init; }
    
    /// <summary>
    /// Тип записи оргструктуры, к которой относится снимок
    /// </summary>
    public string EntityType { get; init; } = string.Empty;
    
    /// <summary>
    /// Идентификатор пользователя, изменившего запись
    /// </summary>
    public Guid ActorId { get; init; }
    
    /// <summary>
    /// Время сохранения исторического снимка в UTC
    /// </summary>
    public DateTime RecordedAt { get; init; }
    
    /// <summary>
    /// Снимок атрибутов записи в JSON
    /// </summary>
    public string Snapshot { get; init; } = string.Empty;
}