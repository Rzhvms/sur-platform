namespace Api.Constants;

/// <summary>
/// Общие значения IdentityHttp для контрактов СУР
/// </summary>
internal static class IdentityHttp
{
    /// <summary>
    /// Csrf header
    /// </summary>
    internal const string CsrfHeader = "X-CSRF-TOKEN";

    /// <summary>
    /// Csrf cookie
    /// </summary>
    internal const string CsrfCookie = "__Host-sur-csrf";

    /// <summary>
    /// Область действия cookie в пределах хоста
    /// </summary>
    internal const string RootPath = "/";

    /// <summary>
    /// Запрет кэширования ответов с учётными данными
    /// </summary>
    internal const string NoStore = "no-store";

    /// <summary>
    /// HTTP-метод изменения состояния
    /// </summary>
    internal const string Post = "POST";

    /// <summary>
    /// Ответ до завершения подготовки схемы
    /// </summary>
    internal const string DatabaseNotReady = "Клиентская база не готова";
}
