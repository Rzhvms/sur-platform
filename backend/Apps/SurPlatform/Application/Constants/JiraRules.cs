namespace Application.Constants;

/// <summary>Состояния и ошибки обработки Jira</summary>
public static class JiraRules
{
    /// <summary>Ожидает отправки</summary>
    public const string Pending = "PENDING";

    /// <summary>Создание отложено до возобновления меры</summary>
    public const string Deferred = "DEFERRED";

    /// <summary>Начало отправки зафиксировано</summary>
    public const string Sending = "SENDING";

    /// <summary>Итог создания требует сверки</summary>
    public const string Unknown = "UNKNOWN";

    /// <summary>Минимальная длина пароля вебхука</summary>
    public const int MinWebhookSecretLength = 24;

    /// <summary>Максимальная длина пароля вебхука в кодовых единицах UTF-16</summary>
    public const int MaxWebhookSecretLength = 256;

    /// <summary>Связь подтверждена</summary>
    public const string Linked = "LINKED";

    /// <summary>Непригодная настройка</summary>
    public const string Invalid = "JIRA_INVALID_CONFIGURATION";

    /// <summary>Запрос Jira не завершился успешно</summary>
    public const string Unavailable = "JIRA_UNAVAILABLE";

    /// <summary>Мера управляется Jira</summary>
    public const string ManualBlocked = "JIRA_MANUAL_CONTROL_BLOCKED";

    /// <summary>Недопустимое разрешение результата</summary>
    public const string Conflict = "JIRA_RECONCILIATION_CONFLICT";

    /// <summary>Схема входящей авторизации</summary>
    public const string Basic = "Basic";

    /// <summary>Внешняя система Jira</summary>
    public const string Source = "Jira";

    /// <summary>Создание задачи</summary>
    public const string Create = "CREATE";

    /// <summary>Jira и завершённая мера имеют разные конечные статусы</summary>
    public const string TerminalMismatch = "JIRA_TERMINAL_MISMATCH";

    /// <summary>Внешний источник назначения</summary>
    public const string AssignmentSource = "jira";

    /// <summary>Внешнее изменение бизнес-сущности</summary>
    public const string Integration = "INTEGRATION";

    /// <summary>Чтение и применение задачи</summary>
    public const string Sync = "SYNC";
}
