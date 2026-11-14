namespace Domain.DalModels;

/// <summary>Данные обмена с Jira в клиентской базе</summary>
public sealed record JiraSettingsDal
{
    /// <summary>Ключ локальной записи</summary>
    public Guid Id { get; init; }

    /// <summary>Версия для конкурентного изменения</summary>
    public Guid Version { get; init; }

    /// <summary>Разрешённый адрес Jira Data Center</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>Ключ проекта Jira</summary>
    public string Project { get; init; } = string.Empty;

    /// <summary>Идентификатор типа задачи</summary>
    public string IssueType { get; init; } = string.Empty;

    /// <summary>Статус Jira для завершённой меры</summary>
    public string DoneStatus { get; init; } = string.Empty;

    /// <summary>Статус Jira для отложенной меры</summary>
    public string? HoldStatus { get; init; }

    /// <summary>Статус Jira для отменённой меры</summary>
    public string? CancelStatus { get; init; }

    /// <summary>Признак активного обмена</summary>
    public bool Enabled { get; init; }

    /// <summary>Ответственный за интеграцию</summary>
    public Guid ResponsibleUserId { get; init; }

    /// <summary>Автор последнего изменения</summary>
    public Guid UpdatedById { get; init; }

    /// <summary>Зашифрованный секрет REST</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string RestSecret { get; init; } = string.Empty;

    /// <summary>Хеш пароля входящего Basic</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public byte[] WebhookHash { get; init; } = [];

    /// <summary>Календарная зона настройки</summary>
    public string TimeZone { get; init; } = string.Empty;

    /// <summary>Соответствия пользователей СУР и Jira</summary>
    public string Executors { get; init; } = string.Empty;
}
