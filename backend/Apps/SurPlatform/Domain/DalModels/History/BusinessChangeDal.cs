namespace Domain.DalModels.History;

/// <summary>
/// Сохраненное событие журнала изменений бизнес-сущностей
/// </summary>
public sealed record BusinessChangeDal
{
    /// <summary>
    /// Уникальный идентификатор события для защиты от повторного переноса
    /// </summary>
    public Guid EventId { get; init; }

    /// <summary>
    /// Время изменения бизнес-сущности в UTC
    /// </summary>
    public DateTime OccurredAt { get; init; }

    /// <summary>
    /// Неизменяемое содержимое события в JSON
    /// </summary>
    public string Payload { get; init; } = string.Empty;
}