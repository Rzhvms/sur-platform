namespace Api.Constants;

/// <summary>
/// Ограничения и подготовка входящих HTTP-сигналов
/// </summary>
internal static class WebhookHttp
{
    /// <summary>
    /// Максимальный размер тела входящего сигнала
    /// </summary>
    internal const int MaxPayloadBytes = 1024 * 1024;

    /// <summary>
    /// Ошибка конфигурации при обходе обязательного фильтра подготовки запроса
    /// </summary>
    internal const string RequestNotPrepared = "HTTP-запрос webhook не подготовлен";
}