using Domain.DalModels;

namespace Application.Models;

/// <summary>Настройка внешнего источника и действующая область</summary>
/// <param name="Settings">Метаданные настройки и ключ адреса в поле id</param>
/// <param name="RiskIds">Настроенные цели</param>
public sealed record ReviewWebhookView(ReviewWebhookDal Settings, Guid[] RiskIds);
