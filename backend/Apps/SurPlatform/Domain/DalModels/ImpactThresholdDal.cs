namespace Domain.DalModels;

/// <summary>Неизменяемая денежная граница уровня</summary>
public sealed record ImpactThresholdDal
{
    /// <summary>Версия шкалы</summary>
    public Guid ScaleId { get; init; }

    /// <summary>Вес тяжести</summary>
    public int Weight { get; init; }

    /// <summary>Включительная нижняя граница</summary>
    public decimal MinLossAmount { get; init; }

    /// <summary>Включительная верхняя граница, null для пустого уровня 0 и неограниченного уровня 5</summary>
    public decimal? MaxLossAmount { get; init; }
}
