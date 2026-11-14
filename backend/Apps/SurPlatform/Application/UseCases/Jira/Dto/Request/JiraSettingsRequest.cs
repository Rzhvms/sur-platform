namespace Application.UseCases.Jira.Dto.Request;

/// <summary>Команда настройки Jira</summary>
public sealed record JiraSettingsRequest
{
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

    /// <summary>Новый токен доступа Jira REST</summary>
    public string? RestToken { get; init; }

    /// <summary>Новый секрет входящего вебхука</summary>
    public string? WebhookSecret { get; init; }

    /// <summary>Соответствия пользователей СУР и Jira</summary>
    public Dictionary<Guid, string> Executors { get; init; } = [];
}
