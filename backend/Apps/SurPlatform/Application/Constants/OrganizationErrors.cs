namespace Application.Constants;

/// <summary>
/// Машиночитаемые ошибки административных команд оргструктуры
/// </summary>
public static class OrganizationErrors
{
    /// <summary>Запись отсутствует в текущей клиентской базе</summary>
    public const string NotFound = "ORGANIZATION_RECORD_NOT_FOUND";

    /// <summary>Запись изменилась после чтения</summary>
    public const string StaleVersion = IdentityErrorCodes.StaleVersion;

    /// <summary>Активная связь блокирует изменение</summary>
    public const string Dependencies = "ORGANIZATION_DEPENDENCIES";

    /// <summary>Пользователь, сотрудник или дирекция недоступны для связи</summary>
    public const string Ineligible = "ORGANIZATION_INELIGIBLE";

    /// <summary>Название, сотрудник или позиция уже зарегистрированы</summary>
    public const string Duplicate = "ORGANIZATION_DUPLICATE";

    /// <summary>Позиция была занята к моменту проверки</summary>
    public const string SlotOccupied = "RESPONSIBILITY_SLOT_OCCUPIED";

    /// <summary>Позицию заняли после проверки</summary>
    public const string SlotChanged = "RESPONSIBILITY_SLOT_CHANGED";

    /// <summary>Недопустимые поля организационной команды</summary>
    public const string Invalid = "ORGANIZATION_INVALID";

    /// <summary>Требуется явное подтверждение замены владельца</summary>
    public const string ConfirmOwner = "OWNER_CHANGE_CONFIRMATION_REQUIRED";

    /// <summary>Администратор не может отключить свою учётную запись</summary>
    public const string SelfDeactivation = "SELF_DEACTIVATION_FORBIDDEN";

    /// <summary>Назначение уже завершено и не возобновляется</summary>
    public const string ClosedAssignment = "RESPONSIBILITY_CLOSED";
}
