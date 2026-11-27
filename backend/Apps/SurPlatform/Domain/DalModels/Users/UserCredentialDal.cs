using Domain.DalModels.Base;

namespace Domain.DalModels.Users;

/// <summary>
/// Учетные данные для проверки пароля пользователя
/// </summary>
public sealed record UserCredentialDal : BaseUserModel
{
    /// <summary>
    /// Хеш пароля пользователя без хранения исходного пароля
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Время последней смены пароля в UTC
    /// </summary>
    public DateTime PasswordChangedAt { get; init; }
}