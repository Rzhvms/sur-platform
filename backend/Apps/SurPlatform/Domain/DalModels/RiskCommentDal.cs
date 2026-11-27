namespace Domain.DalModels;

/// <summary>Данные RiskCommentDal в клиентской базе</summary>
public sealed record RiskCommentDal
{
    /// <summary>Идентификатор записи</summary>
    public Guid Id { get; init; }

    /// <summary>Ожидаемая версия при изменении</summary>
    public Guid Version { get; init; }

    /// <summary>Риск</summary>
    public Guid RiskId { get; init; }

    /// <summary>Автор</summary>
    public Guid AuthorId { get; init; }

    /// <summary>Текст</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Системный комментарий</summary>
    public bool IsSystem { get; init; }

    /// <summary>Логическое удаление</summary>
    public bool IsDeleted { get; init; }

    /// <summary>Время удаления</summary>
    public DateTime? DeletedAt { get; init; }

    /// <summary>Инициатор удаления</summary>
    public Guid? DeletedById { get; init; }

    /// <summary>Время создания UTC</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Время последней правки текста UTC</summary>
    public DateTime UpdatedAt { get; init; }
}
