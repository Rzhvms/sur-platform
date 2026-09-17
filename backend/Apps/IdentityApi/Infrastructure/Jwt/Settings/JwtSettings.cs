using System.ComponentModel.DataAnnotations;

namespace IdentityApi.Infrastructure.Jwt.Settings;

/// <summary>
/// Настройки выпуска и проверки JWT
/// </summary>
public sealed record JwtSettings
{
    /// <summary>
    /// Имя секции конфигурации JWT
    /// </summary>
    public const string SectionName = "Jwt";

    /// <summary>
    /// Издатель JWT
    /// </summary>
    [Required, MinLength(1)]
    public string Issuer { get; init; } = null!;

    /// <summary>
    /// Получатель JWT
    /// </summary>
    [Required, MinLength(1)]
    public string Audience { get; init; } = null!;

    /// <summary>
    /// Время жизни access-токена в секундах
    /// </summary>
    [Range(1, 86_400)]
    public int AccessTokenLifetimeSeconds { get; init; } = 3600;

    /// <summary>
    /// Длина refresh-токена в байтах
    /// </summary>
    [Range(32, 128)]
    public int RefreshTokenLengthBytes { get; init; } = 64;

    /// <summary>
    /// Время жизни refresh-токена в минутах
    /// </summary>
    [Range(1, 525_600)]
    public int RefreshTokenLifetimeMinutes { get; init; } = 10_080;
}