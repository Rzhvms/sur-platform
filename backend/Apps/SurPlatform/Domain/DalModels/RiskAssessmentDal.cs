namespace Domain.DalModels;

/// <summary>Данные RiskAssessmentDal в клиентской базе</summary>
public sealed record RiskAssessmentDal
{
    /// <summary>Идентификатор записи</summary>
    public Guid Id { get; init; }

    /// <summary>Ожидаемая версия при изменении</summary>
    public Guid Version { get; init; }

    /// <summary>Риск</summary>
    public Guid RiskId { get; init; }

    /// <summary>Уровень вероятности</summary>
    public Guid LikelihoodLevelId { get; init; }

    /// <summary>Уровень тяжести</summary>
    public Guid ImpactLevelId { get; init; }

    /// <summary>Оценка</summary>
    public decimal RiskScore { get; init; }

    /// <summary>Текущая оценка</summary>
    public bool IsActive { get; init; }

    /// <summary>Время создания UTC</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Режим обработки</summary>
    public string? ResponseMode { get; init; }

    /// <summary>Метод расчёта</summary>
    public Guid? MethodId { get; init; }

    /// <summary>Точная версия денежной шкалы</summary>
    public Guid? ScaleVersionId { get; init; }

    /// <summary>Исполнитель расчёта</summary>
    public Guid? CreatedById { get; init; }

    /// <summary>Округлённая денежная сумма</summary>
    public decimal? LossAmount { get; init; }

    /// <summary>Исходные недели простоя</summary>
    public int? DowntimeWeeks { get; init; }

    /// <summary>Исходные дни простоя</summary>
    public int? DowntimeDays { get; init; }

    /// <summary>Исходные часы простоя</summary>
    public decimal? DowntimeHours { get; init; }

    /// <summary>Полная продолжительность простоя в часах</summary>
    public decimal? DowntimeTotalHours { get; init; }

    /// <summary>Ставка объекта на момент расчёта</summary>
    public decimal? HourlyDowntimeCostSnapshot { get; init; }
}
