namespace Domain.DalModels.History;

/// <summary>
/// Поручение переноса события в журнал, сохраненное в бизнес-транзакции
/// </summary>
public sealed record BusinessChangeJobDal
{
    /// <summary>
    /// Идентификатор события, сохраняемый при переносе в журнал
    /// </summary>
    public Guid EventId { get; init; }

    /// <summary>
    /// Время изменения бизнес-сущности в UTC
    /// </summary>
    public DateTime OccurredAt { get; init; }

    /// <summary>
    /// Содержимое события для переноса в журнал в JSON
    /// </summary>
    public string Payload { get; init; } = string.Empty;
}