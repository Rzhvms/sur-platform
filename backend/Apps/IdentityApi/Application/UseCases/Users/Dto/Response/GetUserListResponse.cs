using System.Text.Json.Serialization;

namespace Application.UseCases.Users.Dto.Response;

/// <summary>
/// Выходная dto-модель получения списка пользователей
/// </summary>
public sealed record GetUserListResponse
{
    /// <summary>
    /// Список пользователей
    /// </summary>
    [JsonPropertyName("userList")]
    public required List<GetUserResponse> UserList { get; init; }
}