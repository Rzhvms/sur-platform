namespace Application.Models;

/// <summary>
/// Выпущенные токены сессии без привязки к HTTP-ответу
/// </summary>
public sealed record SessionTokens
{
    /// <summary>
    /// Access-токен сессии
    /// </summary>
    public string AccessToken { get; init; }

    /// <summary>
    /// Refresh-токен сессии
    /// </summary>
    public string RefreshToken { get; init; }

    /// <summary>
    /// Время окончания действия refresh-токена
    /// </summary>
    public DateTime RefreshTokenExpiresAt { get; init; }

    ///
    public SessionTokens(string AccessToken, string RefreshToken, DateTime RefreshTokenExpiresAt)
    {
        this.AccessToken = AccessToken;
        this.RefreshToken = RefreshToken;
        this.RefreshTokenExpiresAt = RefreshTokenExpiresAt;
    }
}
