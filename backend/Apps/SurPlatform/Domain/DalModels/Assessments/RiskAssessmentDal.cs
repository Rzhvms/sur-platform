using Domain.DalModels.Base;

namespace Domain.DalModels.Assessments;

/// <summary>
/// Версия оценки риска с сохраненными входами и результатами расчета
/// </summary>
public sealed record RiskAssessmentDal : VersionedDalModelBase
{
    /// <summary>
    /// Идентификатор оцениваемого риска
    /// </summary>
    public Guid RiskId { get; init; }

    /// <summary>
    /// Идентификатор выбранного уровня вероятности
    /// </summary>
    public Guid LikelihoodLevelId { get; init; }

    /// <summary>
    /// Идентификатор рассчитанного уровня тяжести последствий
    /// </summary>
    public Guid ImpactLevelId { get; init; }

    /// <summary>
    /// Итоговый балл, рассчитанный по вероятности и тяжести последствий
    /// </summary>
    public decimal RiskScore { get; init; }

    /// <summary>
    /// Признак текущей оценки риска
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Время создания версии оценки в UTC
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Режим реагирования, связанный с этой версией оценки
    /// </summary>
    public string? ResponseMode { get; init; }

    /// <summary>
    /// Идентификатор использованного метода расчета
    /// </summary>
    public Guid? MethodId { get; init; }

    /// <summary>
    /// Идентификатор денежной шкалы, использованной при расчете
    /// </summary>
    public Guid? ScaleVersionId { get; init; }

    /// <summary>
    /// Идентификатор пользователя, создавшего версию оценки
    /// </summary>
    public Guid? CreatedById { get; init; }

    /// <summary>
    /// Денежный ущерб в рублях после округления
    /// </summary>
    public decimal? LossAmount { get; init; }

    /// <summary>
    /// Количество недель простоя, введенное для расчета
    /// </summary>
    public int? DowntimeWeeks { get; init; }

    /// <summary>
    /// Количество дней простоя, введенное для расчета
    /// </summary>
    public int? DowntimeDays { get; init; }

    /// <summary>
    /// Количество часов простоя, введенное для расчета
    /// </summary>
    public decimal? DowntimeHours { get; init; }

    /// <summary>
    /// Суммарная продолжительность простоя, приведенная к часам
    /// </summary>
    public decimal? DowntimeTotalHours { get; init; }

    /// <summary>
    /// Стоимость часа простоя объекта в рублях на момент расчета
    /// </summary>
    public decimal? HourlyDowntimeCostSnapshot { get; init; }
}