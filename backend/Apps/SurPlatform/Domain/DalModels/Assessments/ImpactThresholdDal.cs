namespace Domain.DalModels.Assessments;

/// <summary>
/// Денежный диапазон уровня тяжести в опубликованной версии шкалы
/// </summary>
public sealed record ImpactThresholdDal
{
    /// <summary>
    /// Идентификатор версии денежной шкалы
    /// </summary>
    public Guid ScaleId { get; init; }

    /// <summary>
    /// Числовой вес уровня тяжести
    /// </summary>
    public int Weight { get; init; }

    /// <summary>
    /// Нижняя граница денежного ущерба в рублях включительно
    /// </summary>
    public decimal MinLossAmount { get; init; }

    /// <summary>
    /// Верхняя граница ущерба в рублях включительно, null для пустого уровня 0 или неограниченного уровня 5
    /// </summary>
    public decimal? MaxLossAmount { get; init; }
}