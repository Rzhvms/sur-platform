using System.Text.Json.Serialization;

namespace Application.UseCases.Users.Dto.Response;

/// <summary>
/// Выходная dto-модель получения списка пользователей
/// </summary>
public sealed record GetUserListResponse
{
    /// <summary>
    /// User list
    /// </summary>
    private const string UserListJsonProperty = "userList";

    /// <summary>
    /// Список пользователей
    /// </summary>
    [JsonPropertyName(UserListJsonProperty)]
    public required List<GetUserResponse> UserList { get; init; }
}
