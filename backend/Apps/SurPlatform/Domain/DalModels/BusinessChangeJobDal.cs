namespace Domain.DalModels;

/// <summary>Данные BusinessChangeJobDal в клиентской базе</summary>
public sealed record BusinessChangeJobDal
{
    /// <summary>Ключ переноса</summary>
    public Guid EventId { get; init; }

    /// <summary>Момент изменения UTC</summary>
    public DateTime OccurredAt { get; init; }

    /// <summary>Событие в JSON</summary>
    public string Payload { get; init; } = string.Empty;
}
