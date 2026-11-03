namespace Application.Models;

/// <summary>
/// Данные для проверки refresh-токена, его сессии и пользователя
/// </summary>
public sealed record RefreshSessionData
{
    /// <summary>
    /// Идентификатор refresh-токена
    /// </summary>
    public Guid TokenId { get; init; }

    /// <summary>
    /// Идентификатор сессии
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// Идентификатор пользователя
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Время окончания действия refresh-токена
    /// </summary>
    public DateTime TokenExpiresAt { get; init; }

    /// <summary>
    /// Время отзыва refresh-токена
    /// </summary>
    public DateTime? TokenRevokedAt { get; init; }

    /// <summary>
    /// Время окончания действия сессии
    /// </summary>
    public DateTime SessionExpiresAt { get; init; }

    /// <summary>
    /// Время отзыва сессии
    /// </summary>
    public DateTime? SessionRevokedAt { get; init; }

    /// <summary>
    /// Признак действующего пользователя
    /// </summary>
    public bool IsUserActive { get; init; }
}