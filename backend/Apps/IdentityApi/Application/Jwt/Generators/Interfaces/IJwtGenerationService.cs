using IdentityApi.Application.Jwt.Models;

namespace IdentityApi.Application.Jwt.Generators.Interfaces;

/// <summary>
/// Сервис генерации и проверки jwt
/// </summary>
public interface IJwtGenerationService
{
    /// <summary>
    /// Генерирует access token
    /// </summary>
    string GenerateAccessToken(JwtUserData user);

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