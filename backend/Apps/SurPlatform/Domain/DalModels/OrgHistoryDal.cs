namespace Domain.DalModels;

/// <summary>
/// Строка оргструктуры клиента
/// </summary>
public sealed record OrgHistoryDal
{
    /// <summary>
    /// Версия сохранённой записи
    /// </summary>
    public Guid Id { get; init; }
    /// <summary>
    /// Идентификатор сущности
    /// </summary>
    public Guid EntityId { get; init; }
    /// <summary>
    /// Тип сущности
    /// </summary>
    public string EntityType { get; init; } = string.Empty;
    /// <summary>
    /// Инициатор изменения
    /// </summary>
    public Guid ActorId { get; init; }
    /// <summary>
    /// Время записи UTC
    /// </summary>
    public DateTime RecordedAt { get; init; }
    /// <summary>
    /// Снимок атрибутов в JSON
    /// </summary>
    public string Snapshot { get; init; } = string.Empty;
}
