using Application.Constants;
using System.Text.Json.Serialization;

namespace Application.UseCases.Users.Dto.Response;

/// <summary>
/// Выходная dto-модель получения пользователя
/// </summary>
public sealed record GetUserResponse
{
    /// <summary>
    /// Role id
    /// </summary>
    private const string RoleIdJsonProperty = "roleId";

    /// <summary>
    /// Идентификатор пользователя
    /// </summary>
    [JsonPropertyName(IdentityJsonProperties.UserId)]
    public Guid Id { get; init; }

    /// <summary>
    /// Электронная почта
    /// </summary>
    [JsonPropertyName(IdentityJsonProperties.Email)]
    public string Email { get; init; } = null!;

    /// <summary>
    /// Идентификатор роли
    /// </summary>
    [JsonPropertyName(RoleIdJsonProperty)]
    public Guid RoleId { get; init; }

    /// <summary>
    /// Имя пользователя
    /// </summary>
    [JsonPropertyName(IdentityJsonProperties.FirstName)]
    public string? FirstName { get; init; }

    /// <summary>
    /// Фамилия пользователя
    /// </summary>
    [JsonPropertyName(IdentityJsonProperties.LastName)]
    public string? LastName { get; init; }

    /// <summary>
    /// Отчество пользователя
    /// </summary>
    [JsonPropertyName(IdentityJsonProperties.Patronymic)]
    public string? Patronymic { get; init; }
}
