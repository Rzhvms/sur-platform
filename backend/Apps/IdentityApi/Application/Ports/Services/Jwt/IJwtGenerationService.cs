using Application.Models;

namespace Application.Ports.Services.Jwt;

/// <summary>
/// Описывает сервис генерации и проверки JWT
/// </summary>
public interface IJwtGenerationService
{
    /// <summary>
    /// Генерирует access token
    /// </summary>
    string GenerateAccessToken(AccessTokenClaims claims, DateTime sessionExpiresAt);

    /// <summary>
    /// Генерирует refresh token
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Вычисляет хеш случайного refresh-токена для хранения в базе
    /// </summary>
    string HashRefreshToken(string token);

    /// <summary>
    /// Возвращает время жизни refresh token в минутах
    /// </summary>
    int GetRefreshTokenLifetimeInMinutes();

    /// <summary>
    /// Извлекает идентификатор пользователя из токена
    /// </summary>
    Guid GetUserIdFromToken(string token);

    /// <summary>
    /// Проверяет валидность токена
    /// </summary>
    bool IsTokenValid(string token, bool validateLifeTime);
}
