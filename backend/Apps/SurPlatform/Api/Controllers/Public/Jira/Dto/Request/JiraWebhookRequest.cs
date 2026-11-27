namespace Api.Controllers.Public.Jira.Dto.Request;

/// <summary>
/// Проверенные реквизиты и идентификатор задачи из входящего сигнала Jira
/// </summary>
internal sealed class JiraWebhookRequest
{
    /// <summary>
    /// Секрет входящего webhook, действующий только в памяти обработки запроса
    /// </summary>
    public required string Secret { get; init; }

    /// <summary>
    /// Идентификатор задачи, которую требуется перечитать через Jira REST
    /// </summary>
    public required string IssueId { get; init; }
}