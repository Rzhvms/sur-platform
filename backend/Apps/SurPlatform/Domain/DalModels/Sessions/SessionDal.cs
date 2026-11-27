using Domain.DalModels.Base;

namespace Domain.DalModels.Sessions;

/// <summary>
/// Авторизационная сессия пользователя с отдельным сроком действия и отзывом
/// </summary>
public sealed record SessionDal : BaseUserModel
{
    /// <summary>
    /// Время создания сессии в UTC
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Время последнего обновления сессии в UTC
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Время окончания действия сессии в UTC
    /// </summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Время отзыва сессии в UTC
    /// </summary>
    public DateTime? RevokedAt { get; init; }

    /// <summary>
    /// Код причины отзыва сессии
    /// </summary>
    public string? RevokeReason { get; init; }

    /// <summary>
    /// Отображаемое название устройства или клиентского приложения
    /// </summary>
    public string? DeviceName { get; init; }
}