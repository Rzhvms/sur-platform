namespace Application.Models;

/// <summary>
/// Данные пользователя для формирования access-токена
/// </summary>
public sealed record AccessTokenClaims
{
    /// <summary>
    /// Идентификатор пользователя
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Идентификатор авторизационной сессии
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// Идентификаторы прав пользователя, полученных через назначенные роли
    /// </summary>
    public IReadOnlyCollection<Guid> RightIdList { get; init; } = [];

    /// <summary>
    /// Версия доступа из клиентской базы
    /// </summary>
    public long AccessVersion { get; init; }

    /// <summary>
    /// Токен разрешает только смену временного пароля
    /// </summary>
    public bool MustChangePassword { get; init; }
}
