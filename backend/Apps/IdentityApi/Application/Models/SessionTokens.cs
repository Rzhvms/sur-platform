namespace Application.Models;

/// <summary>
/// Выпущенные токены сессии без привязки к HTTP-ответу
/// </summary>
public readonly record struct SessionTokens
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

    /// <summary>
    /// Создаёт пару токенов сессии
    /// </summary>
    /// <param name="AccessToken">Access-токен сессии</param>
    /// <param name="RefreshToken">Refresh-токен сессии</param>
    /// <param name="RefreshTokenExpiresAt">Время окончания действия refresh-токена</param>
    public SessionTokens(string AccessToken, string RefreshToken, DateTime RefreshTokenExpiresAt)
    {
        this.AccessToken = AccessToken;
        this.RefreshToken = RefreshToken;
        this.RefreshTokenExpiresAt = RefreshTokenExpiresAt;
    }
}
