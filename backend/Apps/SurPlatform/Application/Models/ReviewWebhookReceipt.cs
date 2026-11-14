using Domain.DalModels;

namespace Application.Models;

/// <summary>Однократная выдача нового секрета</summary>
/// <param name="Webhook">Настройка выданного секрета</param>
/// <param name="Token">Новый секрет только в ответе на выдачу</param>
public sealed record ReviewWebhookReceipt(ReviewWebhookView Webhook, string Token);
