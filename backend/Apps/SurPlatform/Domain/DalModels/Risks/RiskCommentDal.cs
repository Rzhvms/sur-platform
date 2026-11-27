using Domain.DalModels.Base;

namespace Domain.DalModels.Risks;

/// <summary>
/// Пользовательский или системный комментарий в обсуждении риска
/// </summary>
public sealed record RiskCommentDal : VersionedDalModelBase
{
    /// <summary>
    /// Идентификатор обсуждаемого риска
    /// </summary>
    public Guid RiskId { get; init; }

    /// <summary>
    /// Идентификатор пользователя или системного субъекта, создавшего комментарий
    /// </summary>
    public Guid AuthorId { get; init; }

    /// <summary>
    /// Содержимое комментария
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Признак комментария, созданного системой при выполнении операции
    /// </summary>
    public bool IsSystem { get; init; }

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

    /// <summary>
    /// Время создания комментария в UTC
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Время последнего изменения текста комментария в UTC
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}