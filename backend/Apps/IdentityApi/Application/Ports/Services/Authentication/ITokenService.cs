using IdentityApi.Application.Models.Authentication;

namespace IdentityApi.Application.Ports.Services.Authentication;

/// <summary>
/// Сервис генерации и проверки jwt
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Генерирует access token
    /// </summary>
    string GenerateAccessToken(AccessTokenData data);

    /// <summary>
    /// Генерирует refresh token
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Возвращает время жизни refresh token в минутах
    /// </summary>
    TimeSpan GetRefreshTokenLifetime();

    /// <summary>
    /// Извлекает идентификатор пользователя из истекшего токена
    /// </summary>
    Guid GetUserIdFromExpiredAccessToken(string token);

    /// <summary>
    /// Проверяет валидность токена
    /// </summary>
    bool IsAccessTokenValid(string token);
}