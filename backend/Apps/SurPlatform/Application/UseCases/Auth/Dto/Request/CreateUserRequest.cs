using Application.Constants;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.UseCases.Auth.Dto.Request;

/// <summary>
/// Входная модель создания пользователя
/// </summary>
public record CreateUserRequest
{
    /// <summary>
    /// Электронная почта. Используется в качестве логина.
    /// </summary>
    [EmailAddress]
    [MaxLength(50)]
    [Required]
    [JsonPropertyName(IdentityJsonProperties.Email)]
    public required string Email { get; init; }

    /// <summary>
    /// Пароль
    /// </summary>
    [Required]
    [MinLength(8)]
    [JsonPropertyName(IdentityJsonProperties.Password)]
    public required string Password { get; init; }

    /// <summary>
    /// Имя
    /// </summary>
    [Required]
    [JsonPropertyName(IdentityJsonProperties.FirstName)]
    [MaxLength(50)]
    public required string FirstName { get; init; }

    /// <summary>
    /// Фамилия
    /// </summary>
    [Required]
    [JsonPropertyName(IdentityJsonProperties.LastName)]
    [MaxLength(50)]
    public required string LastName { get; init; }

    /// <summary>
    /// Отчество пользователя.
    /// </summary>
    [MaxLength(50)]
    [JsonPropertyName(IdentityJsonProperties.Patronymic)]
    public string? Patronymic { get; init; }
}
