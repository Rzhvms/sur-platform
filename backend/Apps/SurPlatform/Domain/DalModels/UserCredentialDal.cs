namespace Domain.DalModels;

/// <summary>
/// Модель данных проверки пароля
/// </summary>
public sealed record UserCredentialDal
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
    /// Хеш пароля
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Время последнего изменения пароля в UTC
    /// </summary>
    public DateTime PasswordChangedAt { get; init; }
}
