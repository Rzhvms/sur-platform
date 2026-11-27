namespace Api.Controllers.Public.Risks.Dto.Request;

/// <summary>
/// Авторизованный сигнал внешнего источника пересмотра
/// </summary>
internal sealed class ReviewWebhookRequest
{
    /// <summary>
    /// Проверенный секрет источника, действующий только в памяти запроса
    /// </summary>
    public required string Token { get; init; }

    /// <summary>
    /// Исходное тело сигнала для сохранения прикладной командой
    /// </summary>
    public required byte[] Payload { get; init; }
}