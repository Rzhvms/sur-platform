using Domain.DalModels.Base;

namespace Domain.DalModels.Risks;

/// <summary>
/// Опасность из справочника рисков
/// </summary>
public sealed record HazardDal : VersionedDalModelBase
{
    /// <summary>
    /// Название опасности
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Описание опасности и условий ее возникновения
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Доступность опасности при создании и изменении связей риска
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Идентификатор группы, к которой относится опасность
    /// </summary>
    public Guid GroupId { get; init; }
}