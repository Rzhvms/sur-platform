namespace Domain.DalModels;

/// <summary>Данные обмена с Jira в клиентской базе</summary>
public sealed record JiraErrorDal
{
    /// <summary>Ключ локальной записи</summary>
    public Guid Id { get; init; }

    /// <summary>Связанная с ошибкой мера</summary>
    public Guid? TreatmentId { get; init; }

    /// <summary>Устойчивый идентификатор внешней задачи</summary>
    public string? IssueId { get; init; }

    /// <summary>Операция, в которой произошёл сбой</summary>
    public string Operation { get; init; } = string.Empty;

    /// <summary>Безопасный код причины ошибки</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Время фиксации ошибки</summary>
    public DateTime OccurredAt { get; init; }

    /// <summary>Ответственный за интеграцию</summary>
    public Guid ResponsibleUserId { get; init; }

    /// <summary>Автор настройки, использованной при обмене</summary>
    public Guid ConfigurationAuthorId { get; init; }
}
