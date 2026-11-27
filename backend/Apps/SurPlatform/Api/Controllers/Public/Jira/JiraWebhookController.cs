using Api.Constants;
using Api.Filters;
using Api.Controllers.Public.Jira.Dto.Request;
using Application.UseCases.Jira.Interfaces;
using Core.Api.Controllers.ControllerTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Public.Jira;

/// <summary>Входящие сигналы Jira для клиента, выбранного по HTTPS-хосту</summary>
/// <param name="useCases">Авторизация и очередь событий</param>
[AllowAnonymous]
[Route("jira/webhook")]
public sealed class JiraWebhookController(IJiraUseCaseManager useCases) : BasePublicController
{
    /// <summary>Идентификатор независимой схемы OpenAPI</summary>
    internal const string OpenApiScheme = "jiraWebhookBasic";

    /// <summary>Читает идентификатор задачи и фиксирует запрос на её повторное чтение</summary>
    /// <returns>Идентификатор принятого сигнала</returns>
    [HttpPost]
    [RequestSizeLimit(WebhookHttp.MaxPayloadBytes)]
    [TypeFilter(typeof(JiraWebhookRequestFilter))]
    [ProducesResponseType<Guid>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<Guid>> ReceiveAsync()
    {
        var request = HttpContext.Items[typeof(JiraWebhookRequest)] as JiraWebhookRequest
            ?? throw new InvalidOperationException(WebhookHttp.RequestNotPrepared);
        var receipt = await useCases.AcceptJiraSignalAsync(request.Secret, request.IssueId);
        return StatusCode(StatusCodes.Status202Accepted, receipt);
    }
}
