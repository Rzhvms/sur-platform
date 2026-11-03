namespace Application.Constants;

/// <summary>
/// Причины завершения авторизационных сессий
/// </summary>
public static class SessionRevokeReasons
{
    /// <summary>
    /// Выход пользователя
    /// </summary>
    public const string Logout = "logout";
    /// <summary>
    /// Смена пароля
    /// </summary>
    public const string PasswordChanged = "password_changed";
    /// <summary>
    /// Повторное использование refresh-токена
    /// </summary>
    public const string RefreshReuse = "refresh_reuse";
}
