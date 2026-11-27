using Domain.DalModels.Base;

namespace Domain.DalModels.Treatments;

/// <summary>
/// Мера обработки риска с расходами, сроком и текущим состоянием
/// </summary>
public sealed record TreatmentDal : VersionedDalModelBase
{
    /// <summary>
    /// Идентификатор риска, для которого создана мера
    /// </summary>
    public Guid RiskId { get; init; }

    /// <summary>
    /// Идентификатор процесса, используемый при проверке занятого назначения
    /// </summary>
    public Guid ProcessId { get; init; }

    /// <summary>
    /// Идентификатор связи риска с процессом, к которой относится мера
    /// </summary>
    public Guid RiskProcessId { get; init; }

    /// <summary>
    /// Название меры обработки риска
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Описание работ по выполнению меры
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Календарная дата срока выполнения меры без преобразования в UTC
    /// </summary>
    public DateTime DueDate { get; init; }

    /// <summary>
    /// Плановые расходы на выполнение меры в рублях
    /// </summary>
    public decimal CostPlanned { get; init; }

    /// <summary>
    /// Фактические расходы на выполнение меры в рублях, если они указаны
    /// </summary>
    public decimal? CostActual { get; init; }

    /// <summary>
    /// Код текущего состояния меры
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Идентификатор пользователя, создавшего меру
    /// </summary>
    public Guid CreatedById { get; init; }

    /// <summary>
    /// Время создания меры в UTC
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Время последнего изменения меры в UTC
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}