namespace Application.Models;

/// <summary>
/// Данные выдачи доступа без исходного пароля
/// </summary>
public sealed record IdentityInvitation
{
    /// <summary>
    /// Получатель доступа
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Версия выдачи
    /// </summary>
    public Guid Version { get; init; }

    /// <summary>
    /// Адрес доставки
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Имя получателя
    /// </summary>
    public string? FullName { get; init; }

    /// <summary>
    /// Назначенные роли
    /// </summary>
    public string[] Roles { get; init; } = [];

    /// <summary>Действующие зоны ответственности на момент выдачи</summary>
    public string[] Responsibilities { get; init; } = [];
}
