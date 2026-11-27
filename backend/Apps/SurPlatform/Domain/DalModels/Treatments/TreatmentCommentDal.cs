using Domain.DalModels.Base;

namespace Domain.DalModels.Treatments;

/// <summary>
/// Комментарий в обсуждении меры с сохранением сведений об удалении
/// </summary>
public sealed record TreatmentCommentDal : VersionedDalModelBase
{
    /// <summary>
    /// Идентификатор обсуждаемой меры
    /// </summary>
    public Guid TreatmentId { get; init; }

    /// <summary>
    /// Идентификатор пользователя, создавшего комментарий
    /// </summary>
    public Guid AuthorId { get; init; }

    /// <summary>
    /// Содержимое комментария к мере
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Время создания комментария в UTC
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Время последнего изменения текста комментария в UTC
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Признак скрытого комментария с сохранением записи в истории
    /// </summary>
    public bool IsDeleted { get; init; }

    /// <summary>
    /// Время логического удаления комментария в UTC
    /// </summary>
    public DateTime? DeletedAt { get; init; }

    /// <summary>
    /// Идентификатор пользователя, удалившего комментарий
    /// </summary>
    public Guid? DeletedById { get; init; }
}