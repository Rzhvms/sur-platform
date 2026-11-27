namespace Domain.DalModels.Reviews;

/// <summary>
/// Связь внешнего источника пересмотра с целевым риском
/// </summary>
public sealed record ReviewWebhookRiskDal
{
    /// <summary>
    /// Идентификатор настройки внешнего источника
    /// </summary>
    public Guid WebhookId { get; init; }

    /// <summary>
    /// Идентификатор риска, выбранного целью пересмотра
    /// </summary>
    public Guid RiskId { get; init; }

    /// <summary>
    /// Время добавления риска в цели источника в UTC
    /// </summary>
    public DateTime LinkedAt { get; init; }

    /// <summary>
    /// Идентификатор администратора, добавившего связь
    /// </summary>
    public Guid LinkedById { get; init; }
}