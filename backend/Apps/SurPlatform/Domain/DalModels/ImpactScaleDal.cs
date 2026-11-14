namespace Domain.DalModels;

/// <summary>Опубликованная версия денежных границ</summary>
public sealed record ImpactScaleDal
{
    /// <summary>Идентификатор версии</summary>
    public Guid Id { get; init; }

    /// <summary>Порядок публикации в базе</summary>
    public long Sequence { get; init; }

    /// <summary>Автор настройки</summary>
    public Guid CreatedById { get; init; }

    /// <summary>Момент публикации</summary>
    public DateTime CreatedAt { get; init; }
}
