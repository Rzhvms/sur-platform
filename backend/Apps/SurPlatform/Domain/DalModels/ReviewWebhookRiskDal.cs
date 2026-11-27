namespace Domain.DalModels;

/// <summary>Действующая связь вебхука и принятого риска</summary>
public sealed record ReviewWebhookRiskDal
{
    /// <summary>Вебхук</summary>
    public Guid WebhookId { get; init; }

    /// <summary>Настроенная цель</summary>
    public Guid RiskId { get; init; }

    /// <summary>Время добавления UTC</summary>
    public DateTime LinkedAt { get; init; }

    /// <summary>Администратор, добавивший связь</summary>
    public Guid LinkedById { get; init; }
}
