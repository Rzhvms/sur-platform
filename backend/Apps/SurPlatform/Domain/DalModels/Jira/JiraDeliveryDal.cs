using Domain.DalModels.Base;

namespace Domain.DalModels.Jira;

/// <summary>
/// Поручение создания задачи Jira и сохраненная связь с мерой
/// </summary>
public sealed record JiraDeliveryDal : DalModelBase
{
    /// <summary>
    /// Состояние отправки поручения или связывания задачи
    /// </summary>
    public string State { get; init; } = string.Empty;

    /// <summary>
    /// Время начала попытки создания задачи в UTC, сохраненное до отправки запроса
    /// </summary>
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// Ближайшее допустимое время повторной попытки в UTC
    /// </summary>
    public DateTime? RetryAt { get; init; }

    /// <summary>
    /// Стабильный идентификатор связанной задачи Jira
    /// </summary>
    public string? IssueId { get; init; }

    /// <summary>
    /// Отображаемый ключ связанной задачи Jira
    /// </summary>
    public string? IssueKey { get; init; }

    /// <summary>
    /// Метка обновления задачи Jira из последнего примененного наблюдения
    /// </summary>
    public DateTimeOffset? ObservedAt { get; init; }

    /// <summary>
    /// Время следующего контрольного чтения задачи в UTC
    /// </summary>
    public DateTime? SyncAt { get; init; }

    /// <summary>
    /// Идентификатор пользователя, инициировавшего создание задачи
    /// </summary>
    public Guid TriggeredBy { get; init; }

    /// <summary>
    /// Признак запуска из риска: true для запуска набора мер, false для ручного возобновления, null для неизвестного источника
    /// </summary>
    public bool? TriggeredFromRisk { get; init; }

    /// <summary>
    /// Код последнего сбоя до начала отправки запроса создания задачи
    /// </summary>
    public string? RetryCode { get; init; }
}