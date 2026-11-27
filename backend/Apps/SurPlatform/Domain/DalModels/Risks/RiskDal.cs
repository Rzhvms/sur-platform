using Domain.DalModels.Base;

namespace Domain.DalModels.Risks;

/// <summary>
/// Паспорт риска и его текущее состояние в жизненном цикле
/// </summary>
public sealed record RiskDal : VersionedDalModelBase
{
    /// <summary>
    /// Название риска
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Описание рискованного события и его возможных последствий
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Идентификатор опасности, с которой связан риск
    /// </summary>
    public Guid HazardId { get; init; }

    /// <summary>
    /// Идентификатор категории риска
    /// </summary>
    public Guid CategoryId { get; init; }

    /// <summary>
    /// Идентификатор пользователя, зарегистрировавшего риск
    /// </summary>
    public Guid AuthorId { get; init; }

    /// <summary>
    /// Идентификатор связанного объекта при его наличии
    /// </summary>
    public Guid? AssetId { get; init; }

    /// <summary>
    /// Текущее состояние риска в жизненном цикле
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Уровень полномочий, необходимый для принятия риска
    /// </summary>
    public string? AcceptanceLevel { get; init; }

    /// <summary>
    /// Сохраненное обоснование закрытия риска
    /// </summary>
    public string? CloseReason { get; init; }

    /// <summary>
    /// Время регистрации риска в UTC
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Время последнего изменения риска в UTC
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}