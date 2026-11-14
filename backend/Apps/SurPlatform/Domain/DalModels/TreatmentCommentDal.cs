namespace Domain.DalModels;

/// <summary>Хранение данных этапа выполнения мер</summary>
public sealed record TreatmentCommentDal
{
    /// <summary>Идентификатор записи</summary>
    public Guid Id { get; init; }

    /// <summary>Версия для конкурентной записи</summary>
    public Guid Version { get; init; }

    /// <summary>Мера</summary>
    public Guid TreatmentId { get; init; }

    /// <summary>Автор комментария</summary>
    public Guid AuthorId { get; init; }

    /// <summary>Текст комментария</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Момент создания UTC</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Момент редактирования UTC</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>Признак логического удаления</summary>
    public bool IsDeleted { get; init; }

    /// <summary>Момент удаления UTC</summary>
    public DateTime? DeletedAt { get; init; }

    /// <summary>Удаливший пользователь</summary>
    public Guid? DeletedById { get; init; }
}
