namespace Domain.DalModels;

/// <summary>Данные BusinessChangeDal в клиентской базе</summary>
public sealed record BusinessChangeDal
{
    /// <summary>Идемпотентный идентификатор события</summary>
    public Guid EventId { get; init; }

    /// <summary>Момент изменения UTC</summary>
    public DateTime OccurredAt { get; init; }

    /// <summary>Неизменяемое событие в JSON</summary>
    public string Payload { get; init; } = string.Empty;
}
