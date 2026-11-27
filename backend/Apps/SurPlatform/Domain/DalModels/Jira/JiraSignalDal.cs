using Domain.DalModels.Base;

namespace Domain.DalModels.Jira;

/// <summary>
/// Сохраненный входящий сигнал для последующего чтения задачи через REST Jira
/// </summary>
public sealed record JiraSignalDal : DalModelBase
{
    /// <summary>
    /// Стабильный идентификатор задачи Jira, которую требуется перечитать
    /// </summary>
    public string IssueId { get; init; } = string.Empty;

    /// <summary>
    /// Время сохранения входящего сигнала в UTC
    /// </summary>
    public DateTime CreatedAt { get; init; }
}