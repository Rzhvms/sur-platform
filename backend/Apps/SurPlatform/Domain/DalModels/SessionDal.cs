namespace Domain.DalModels;

/// <summary>
/// Модель авторизационной сессии пользователя
/// </summary>
public sealed record SessionDal
{
    /// <summary>
    /// Идентификатор записи
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Идентификатор пользователя
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Время создания сессии в UTC
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Время последнего обновления сессии в UTC
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Время окончания сессии в UTC
    /// </summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Время отзыва сессии в UTC
    /// </summary>
    public DateTime? RevokedAt { get; init; }

    /// <summary>
    /// Причина отзыва сессии
    /// </summary>
    public string? RevokeReason { get; init; }

    /// <summary>
    /// Отображаемое название устройства
    /// </summary>
    public string? DeviceName { get; init; }

    /// <summary>
    /// Версия для контроля конкурентных изменений
    /// </summary>
    public Guid Version { get; init; }
}
