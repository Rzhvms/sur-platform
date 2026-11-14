namespace Domain.DalModels;

/// <summary>Данные обмена с Jira в клиентской базе</summary>
public sealed record JiraDeliveryDal
{
    /// <summary>Ключ локальной записи</summary>
    public Guid Id { get; init; }

    /// <summary>Состояние создания или привязки</summary>
    public string State { get; init; } = string.Empty;

    /// <summary>Сохранённое начало попытки отправки</summary>
    public DateTime? StartedAt { get; init; }

    /// <summary>Ближайшее допустимое время повтора</summary>
    public DateTime? RetryAt { get; init; }

    /// <summary>Устойчивый идентификатор внешней задачи</summary>
    public string? IssueId { get; init; }

    /// <summary>Текущий ключ внешней задачи</summary>
    public string? IssueKey { get; init; }

    /// <summary>Версия последнего наблюдения Jira</summary>
    public DateTimeOffset? ObservedAt { get; init; }

    /// <summary>Время следующего контрольного чтения</summary>
    public DateTime? SyncAt { get; init; }

    /// <summary>Инициатор создания задачи</summary>
    public Guid TriggeredBy { get; init; }
}
