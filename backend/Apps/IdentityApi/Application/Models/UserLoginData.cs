namespace Application.Models;

/// <summary>
/// Данные, необходимые для проверки учётных данных пользователя
/// </summary>
public sealed record UserLoginData
{
    /// <summary>
    /// Идентификатор пользователя
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Хеш пароля
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Признак действующего пользователя
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Время окончания временной блокировки входа
    /// </summary>
    public DateTime? LockoutUntil { get; init; }
}
