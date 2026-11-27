using Domain.DalModels.Base;

namespace Domain.DalModels.Jira;

/// <summary>
/// Запись журнала ошибок обмена с Jira с контекстом ответственности
/// </summary>
public sealed record JiraErrorDal : DalModelBase
{
    /// <summary>
    /// Идентификатор меры, при обработке которой возникла ошибка
    /// </summary>
    public Guid? TreatmentId { get; init; }

    /// <summary>
    /// Стабильный идентификатор задачи Jira, связанной с ошибкой
    /// </summary>
    public string? IssueId { get; init; }

    /// <summary>
    /// Код операции интеграции, завершившейся ошибкой
    /// </summary>
    public string Operation { get; init; } = string.Empty;

    /// <summary>
    /// Безопасный код причины ошибки без ответа Jira и секретов
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Время регистрации ошибки в UTC
    /// </summary>
    public DateTime OccurredAt { get; init; }

    /// <summary>
    /// Идентификатор ответственного за интеграцию на момент ошибки
    /// </summary>
    public Guid ResponsibleUserId { get; init; }

    /// <summary>
    /// Идентификатор автора настройки, использованной при обмене
    /// </summary>
    public Guid ConfigurationAuthorId { get; init; }

    /// <summary>
    /// Ключ подтвержденной задачи Jira при совпадении с задачей события
    /// </summary>
    public string? IssueKey { get; init; }

    /// <summary>
    /// Текущее состояние повторной обработки связанной меры
    /// </summary>
    public string? RetryState { get; init; }
}