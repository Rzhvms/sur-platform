namespace Api.Constants;

/// <summary>
/// Общие значения IdentityRoutes для контрактов СУР
/// </summary>
internal static class IdentityRoutes
{
    /// <summary>
    /// Connect
    /// </summary>
    internal const string Connect = "connect";

    /// <summary>
    /// Users
    /// </summary>
    internal const string Users = "users";

    /// <summary>
    /// Token
    /// </summary>
    internal const string Token = "token";

    /// <summary>
    /// Revoke token
    /// </summary>
    internal const string RevokeToken = "token/revoke";

    /// <summary>
    /// Refresh token
    /// </summary>
    internal const string RefreshToken = "token/refresh";

    /// <summary>
    /// Change password
    /// </summary>
    internal const string ChangePassword = "change-password";

    /// <summary>
    /// Health
    /// </summary>
    internal const string Health = "health";

    /// <summary>
    /// Ready
    /// </summary>
    internal const string Ready = "ready";

    /// <summary>Проверка процесса без обращения к базе</summary>
    internal const string Live = "live";

    /// <summary>Абсолютный путь проверки процесса</summary>
    internal const string LivenessPath = IdentityHttp.RootPath + Health + IdentityHttp.RootPath + Live;

    /// <summary>
    /// Полный путь проверки готовности клиентской базы
    /// </summary>
    internal const string ReadinessPath = IdentityHttp.RootPath + Health + IdentityHttp.RootPath + Ready;
}
