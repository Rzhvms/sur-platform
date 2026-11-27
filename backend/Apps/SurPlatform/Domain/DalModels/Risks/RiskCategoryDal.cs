using Domain.DalModels.Base;

namespace Domain.DalModels.Risks;

/// <summary>
/// Категория для классификации рисков
/// </summary>
public sealed record RiskCategoryDal : VersionedDalModelBase
{
    /// <summary>
    /// Название категории риска
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Описание категории риска
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Доступность категории при создании и изменении связей риска
    /// </summary>
    public bool IsActive { get; init; }
}