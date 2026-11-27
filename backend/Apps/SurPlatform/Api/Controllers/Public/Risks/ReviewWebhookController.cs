using Api.Constants;
using Api.Filters;
using Api.Controllers.Public.Risks.Dto.Request;
using Application.Models;
using Application.UseCases.Risks.Interfaces;
using Core.Api.Controllers.ControllerTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Public.Risks;

/// <summary>Принимает авторизованные сигналы о пересмотре в клиентской базе</summary>
/// <param name="useCases">Приём и сохранение доверенного сигнала</param>
[Route("review-webhooks")]
[AllowAnonymous]
public sealed class ReviewWebhookController(IReviewUseCaseManager useCases) : BasePublicController
{
    /// <summary>Название отдельной схемы авторизации в OpenAPI</summary>
    internal const string OpenApiScheme = "reviewWebhookBearer";

    /// <summary>Доверяет только HTTPS и активному секрету выбранной клиентской базы</summary>
    /// <param name="id">Публичный ключ адреса</param>
    /// <returns>Идентификатор зафиксированного запуска</returns>
    [HttpPost("{id:guid}")]
    [RequestSizeLimit(WebhookHttp.MaxPayloadBytes)]
    [TypeFilter(typeof(ReviewWebhookRequestFilter))]
    [ProducesResponseType<ReviewWebhookAccepted>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<ReviewWebhookAccepted>> ReceiveAsync(Guid id)
    {
        var request = HttpContext.Items[typeof(ReviewWebhookRequest)] as ReviewWebhookRequest
            ?? throw new InvalidOperationException(WebhookHttp.RequestNotPrepared);
        var receipt = await useCases.AcceptReviewWebhookAsync(id, request.Token, request.Payload);
        return StatusCode(StatusCodes.Status202Accepted, receipt);
    }
}
