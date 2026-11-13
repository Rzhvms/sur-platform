namespace Application.Models;

/// <summary>
/// Состояние доступа, прочитанное из клиентской базы
/// </summary>
public sealed record IdentityAccess
{
    /// <summary>
    /// Версия применимых прав и учётных данных
    /// </summary>
    public long AccessVersion { get; init; }

    /// <summary>
    /// До смены пароля доступ ограничен
    /// </summary>
    public bool MustChangePassword { get; init; }

    /// <summary>
    /// Срок временного пароля
    /// </summary>
    public DateTime? TemporaryPasswordExpiresAt { get; init; }

    /// <summary>
    /// Эффективные права
    /// </summary>
    public Guid[] RightIds { get; init; } = [];
}
