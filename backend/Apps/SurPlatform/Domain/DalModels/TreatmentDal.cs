namespace Domain.DalModels;

/// <summary>Хранение данных этапа выполнения мер</summary>
public sealed record TreatmentDal
{
    /// <summary>Идентификатор записи</summary>
    public Guid Id { get; init; }

    /// <summary>Версия для конкурентной записи</summary>
    public Guid Version { get; init; }

    /// <summary>Риск</summary>
    public Guid RiskId { get; init; }

    /// <summary>Стабильный процесс для ограничения слота</summary>
    public Guid ProcessId { get; init; }

    /// <summary>Связь с процессом риска</summary>
    public Guid RiskProcessId { get; init; }

    /// <summary>Название меры</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Описание работ</summary>
    public string? Description { get; init; }

    /// <summary>Календарная дата срока</summary>
    public DateTime DueDate { get; init; }

    /// <summary>Плановая стоимость в рублях</summary>
    public decimal CostPlanned { get; init; }

    /// <summary>Фактическая стоимость в рублях</summary>
    public decimal? CostActual { get; init; }

    /// <summary>Состояние меры</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Автор меры</summary>
    public Guid CreatedById { get; init; }

    /// <summary>Момент создания UTC</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Момент изменения UTC</summary>
    public DateTime UpdatedAt { get; init; }
}
