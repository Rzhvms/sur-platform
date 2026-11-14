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

    /// <summary>
    /// Письмо ожидает подтверждения доставки
    /// </summary>
    public const string InvitationPending = "PENDING";

    /// <summary>
    /// Почтовый сервер принял письмо
    /// </summary>
    public const string InvitationSent = "SENT";

    /// <summary>
    /// Доставка доступа не подтверждена
    /// </summary>
    public const string InvitationFailed = "FAILED";
}
