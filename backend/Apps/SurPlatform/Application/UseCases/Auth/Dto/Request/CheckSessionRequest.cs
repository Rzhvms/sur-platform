namespace Application.UseCases.Auth.Dto.Request;

/// <summary>
/// Сессия и право для проверки внутренней операции
/// </summary>
public sealed record CheckSessionRequest
{
    /// <summary>
    /// Идентификатор пользователя
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// Идентификатор сессии
    /// </summary>
    public required Guid SessionId { get; init; }

    /// <summary>
    /// Версия доступа из проверенного токена
    /// </summary>
    public required long AccessVersion { get; init; }

    /// <summary>
    /// Системный код права, требуемого операцией
    /// </summary>
    public required string RightCode { get; init; }
}