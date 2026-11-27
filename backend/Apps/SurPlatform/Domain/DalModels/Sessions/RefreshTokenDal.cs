using Domain.DalModels.Base;

namespace Domain.DalModels.Sessions;

/// <summary>
/// Запись refresh-токена с хешем секрета и историей ротации
/// </summary>
public sealed record RefreshTokenDal : DalModelBase
{
    /// <summary>
    /// Идентификатор сессии, для которой выпущен токен
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// Хеш случайного refresh-токена без хранения исходного секрета
    /// </summary>
    public string TokenHash { get; init; } = string.Empty;

    /// <summary>
    /// Время выпуска refresh-токена в UTC
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Время окончания действия refresh-токена в UTC
    /// </summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Время отзыва refresh-токена в UTC
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Время использования токена для ротации в UTC
    /// </summary>
    public DateTime? UsedAt { get; init; }

    /// <summary>
    /// Идентификатор refresh-токена, выпущенного взамен использованного
    /// </summary>
    public Guid? ReplacedById { get; init; }
}