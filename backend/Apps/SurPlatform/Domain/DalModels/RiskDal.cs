namespace Domain.DalModels;

/// <summary>Данные RiskDal в клиентской базе</summary>
public sealed record RiskDal
{
    /// <summary>Идентификатор записи</summary>
    public Guid Id { get; init; }

    /// <summary>Ожидаемая версия при изменении</summary>
    public Guid Version { get; init; }

    /// <summary>Название риска</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Описание риска</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Опасность</summary>
    public Guid HazardId { get; init; }

    /// <summary>Категория</summary>
    public Guid CategoryId { get; init; }

    /// <summary>Автор</summary>
    public Guid AuthorId { get; init; }

    /// <summary>Объект</summary>
    public Guid? AssetId { get; init; }

    /// <summary>Текущий статус</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Маршрут принятия</summary>
    public string? AcceptanceLevel { get; init; }

    /// <summary>Причина закрытия</summary>
    public string? CloseReason { get; init; }

    /// <summary>Время создания UTC</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Время изменения UTC</summary>
    public DateTime UpdatedAt { get; init; }
}
