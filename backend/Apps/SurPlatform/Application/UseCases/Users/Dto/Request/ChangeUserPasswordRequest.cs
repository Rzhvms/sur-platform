using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Application.UseCases.Users.Dto.Request;

/// <summary>
/// Входная модель изменения пароля
/// </summary>
public record ChangeUserPasswordRequest
{
    /// <summary>
    /// New password
    /// </summary>
    private const string NewPasswordJsonProperty = "newPassword";

    /// <summary>
    /// Old password
    /// </summary>
    private const string OldPasswordJsonProperty = "oldPassword";

    /// <summary>
    /// Новый пароль
    /// </summary>
    [JsonPropertyName(NewPasswordJsonProperty)]
    [Required]
    [StringLength(256, MinimumLength = 8)]
    public required string NewPassword { get; init; }

    /// <summary>
    /// Старый пароль
    /// </summary>
    [JsonPropertyName(OldPasswordJsonProperty)]
    [Required]
    [StringLength(256, MinimumLength = 8)]
    public required string OldPassword { get; init; }
}
