namespace Application.Constants;

/// <summary>
/// Зарезервированные идентификаторы и состояния выдачи
/// </summary>
public static class IdentityAccounts
{
    /// <summary>
    /// Зарезервированный GUID системного пользователя
    /// </summary>
    private const string SystemUserIdentifier = "00000000-0000-0000-0000-000000000001";

    /// <summary>
    /// Системный субъект для технических действий
    /// </summary>
    public static readonly Guid SystemUserId = new(SystemUserIdentifier);

    /// <summary>
    /// Код роли администратора при начальной выдаче
    /// </summary>
    public const string AdminRole = "ADMIN";

    /// <summary>
    /// Срок действия временного пароля в календарных днях
    /// </summary>
    public const int TemporaryPasswordDays = 10;

    /// <summary>Минимальная длина временного пароля</summary>
    public const int MinPasswordLength = 8;

    /// <summary>Верхняя граница длины пароля</summary>
    public const int MaxPasswordLength = 256;

    /// <summary>
    /// Служебная команда настроила пароль администратора из конфигурации
    /// </summary>
    public const string DevelopmentAccessConfigured = "CONFIGURED";
}
