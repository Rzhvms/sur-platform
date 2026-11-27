namespace Application.Constants;

/// <summary>
/// Общие значения IdentityMessages для контрактов СУР
/// </summary>
public static class IdentityMessages
{
    /// <summary>
    /// Общая причина отказа при некорректных реквизитах аутентификации
    /// </summary>
    public const string InvalidCredentials = "Неверная почта или пароль";

    /// <summary>
    /// Сообщение о недействительной сессии
    /// </summary>
    public const string InvalidSession = "Сессия недействительна";
}
