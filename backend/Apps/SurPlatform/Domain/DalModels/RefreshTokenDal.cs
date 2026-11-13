namespace Domain.DalModels;

/// <summary>
/// Модель токена продления сессии
/// </summary>
public sealed record RefreshTokenDal
{
    /// <summary>
    /// Идентификатор записи
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Идентификатор сессии
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// Хеш случайного токена
    /// </summary>
    public string TokenHash { get; init; } = string.Empty;

    /// <summary>
    /// Время создания в UTC
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Время окончания действия в UTC
    /// </summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Время отзыва в UTC
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Момент ротации токена
    /// </summary>
    public DateTime? UsedAt { get; init; }

    /// <summary>
    /// Токен, выпущенный при ротации
    /// </summary>
    public Guid? ReplacedById { get; init; }
}
