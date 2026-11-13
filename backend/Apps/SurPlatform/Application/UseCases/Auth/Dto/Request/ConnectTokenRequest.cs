using Application.Constants;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.UseCases.Auth.Dto.Request;

/// <summary>
/// Входная модель авторизации
/// </summary>
public record ConnectTokenRequest
{
    /// <summary>
    /// Электронная почта, по которой будет произведена авторизация
    /// </summary>
    [Required]
    [MaxLength(254)]
    [JsonPropertyName(IdentityJsonProperties.Email)]
    [EmailAddress]
    public required string Email { get; set; }

    /// <summary>
    /// Пароль
    /// </summary>
    [Required]
    [StringLength(256, MinimumLength = 8)]
    [JsonPropertyName(IdentityJsonProperties.Password)]
    public required string Password { get; set; }
}
