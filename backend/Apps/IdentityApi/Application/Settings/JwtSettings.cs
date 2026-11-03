using System.ComponentModel.DataAnnotations;

namespace Application.Settings;

/// <summary>
/// Настройки JWT
/// </summary>
public sealed class JwtSettings
{
    /// <summary>
    /// Настройки access token
    /// </summary>
    [Required]
    public required AccessTokenSettings AccessTokenSettings { get; init; }

    /// <summary>
    /// Настройки refresh token
    /// </summary>
    [Required]
    public required RefreshTokenSettings RefreshTokenSettings { get; init; }
}