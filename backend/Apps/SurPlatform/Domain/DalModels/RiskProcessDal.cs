namespace Domain.DalModels;

/// <summary>Данные RiskProcessDal в клиентской базе</summary>
public sealed record RiskProcessDal
{
    /// <summary>Идентификатор записи</summary>
    public Guid Id { get; init; }

    /// <summary>Ожидаемая версия при изменении</summary>
    public Guid Version { get; init; }

    /// <summary>Риск</summary>
    public Guid RiskId { get; init; }

    /// <summary>Процесс</summary>
    public Guid ProcessId { get; init; }

    /// <summary>Сотрудник-владелец</summary>
    public Guid OwnerId { get; init; }

    /// <summary>Основная связь</summary>
    public bool IsPrimary { get; init; }

    /// <summary>Время начала связи UTC</summary>
    public DateTime LinkedAt { get; init; }

    /// <summary>Время окончания связи UTC</summary>
    public DateTime? UnlinkedAt { get; init; }
}
