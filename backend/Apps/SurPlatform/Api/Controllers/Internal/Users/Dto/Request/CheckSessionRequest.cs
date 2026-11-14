using System.ComponentModel.DataAnnotations;

namespace Api.Controllers.Internal.Users.Dto.Request;

/// <summary>
/// Сессия и право для проверки внутренней операции
/// </summary>
public sealed record CheckSessionRequest
{
    /// <summary>
    /// Пользователь из проверенного токена
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// Сессия из проверенного токена
    /// </summary>
    public required Guid SessionId { get; init; }

    /// <summary>
    /// Версия доступа из проверенного токена
    /// </summary>
    public required long AccessVersion { get; init; }

    /// <summary>
    /// Системный код права, требуемого операцией
    /// </summary>
    [Required]
    public required string RightCode { get; init; }
}
