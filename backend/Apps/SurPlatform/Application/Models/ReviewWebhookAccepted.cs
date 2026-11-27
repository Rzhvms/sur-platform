namespace Application.Models;

/// <summary>Подтверждение сохранённого обращения</summary>
/// <param name="ReviewRunId">Идентификатор, который использует фоновая обработка</param>
public sealed record ReviewWebhookAccepted(Guid ReviewRunId);
