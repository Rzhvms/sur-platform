using Domain.DalModels.Base;

namespace Domain.DalModels;

/// <summary>
/// Настройки единственного подключения клиента к Jira Data Center
/// </summary>
public sealed record JiraSettingsDal : VersionedDalModelBase
{
    /// <summary>
    /// Разрешенный базовый адрес Jira Data Center
    /// </summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Ключ проекта Jira для создаваемых задач
    /// </summary>
    public string Project { get; init; } = string.Empty;

    /// <summary>
    /// Идентификатор типа создаваемой задачи Jira
    /// </summary>
    public string IssueType { get; init; } = string.Empty;

    /// <summary>
    /// Идентификатор статуса Jira, соответствующего завершению меры
    /// </summary>
    public string DoneStatus { get; init; } = string.Empty;

    /// <summary>
    /// Идентификатор статуса Jira, соответствующего приостановке меры
    /// </summary>
    public string? HoldStatus { get; init; }

    /// <summary>
    /// Идентификатор статуса Jira, соответствующего отмене меры
    /// </summary>
    public string? CancelStatus { get; init; }

    /// <summary>
    /// Признак включенного обмена с Jira
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Идентификатор пользователя, ответственного за интеграцию
    /// </summary>
    public Guid ResponsibleUserId { get; init; }

    /// <summary>
    /// Идентификатор администратора, последним изменившего настройку
    /// </summary>
    public Guid UpdatedById { get; init; }

    /// <summary>
    /// Идентификатор создателя настройки, если он сохранен
    /// </summary>
    public Guid? CreatedById { get; init; }

    /// <summary>
    /// Идентификатор администратора, последним включившего интеграцию с восстановлением очереди
    /// </summary>
    public Guid? RestoredById { get; init; }

    /// <summary>
    /// Зашифрованные учетные данные исходящих REST-запросов, исключенные из JSON-ответов
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string RestSecret { get; init; } = string.Empty;

    /// <summary>
    /// Хеш пароля отдельной Basic-аутентификации входящего webhook, исключенный из JSON-ответов
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public byte[] WebhookHash { get; init; } = [];

    /// <summary>
    /// Календарная зона для преобразования срока меры в дату Jira
    /// </summary>
    public string TimeZone { get; init; } = string.Empty;

    /// <summary>
    /// Сопоставление пользователей СУР с исполнителями Jira в JSON
    /// </summary>
    public string Executors { get; init; } = string.Empty;
}
