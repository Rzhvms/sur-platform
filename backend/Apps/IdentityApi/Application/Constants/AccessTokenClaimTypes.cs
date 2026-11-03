namespace Application.Constants;

/// <summary>
/// Общие имена claims авторизационной сессии и прав пользователя
/// </summary>
public static class AccessTokenClaimTypes
{
    /// <summary>
    /// Идентификатор авторизационной сессии
    /// </summary>
    public const string SessionId = "sid";

    /// <summary>
    /// Идентификатор права пользователя
    /// </summary>
    public const string Right = "right";
}