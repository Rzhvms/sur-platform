using System.Net.Http.Headers;
using Api.Constants;
using Application.Constants;
using Application.Models;
using Application.UseCases.Risks;
using Core.Api.Controllers.ControllerTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Public.Risks;

/// <summary>Принимает авторизованные сигналы о пересмотре в клиентской базе</summary>
/// <param name="useCases">Приём и сохранение доверенного сигнала</param>
[Route(WebhookRoute)]
[AllowAnonymous]
public sealed class ReviewWebhookController(ReviewUseCaseManager useCases) : BasePublicController
{
    /// <summary>Название отдельной схемы авторизации в OpenAPI</summary>
    internal const string OpenApiScheme = "reviewWebhookBearer";

    /// <summary>Относительный адрес отдельного внешнего источника</summary>
    private const string WebhookRoute = "review-webhooks";

    /// <summary>Схема отдельного секрета по HTTP</summary>
    private const string BearerScheme = "Bearer";

    /// <summary>Максимальное тело сигнала в байтах</summary>
    private const int MaxPayloadBytes = 1024 * 1024;

    /// <summary>Доверяет только HTTPS и активному секрету выбранной клиентской базы</summary>
    /// <param name="id">Публичный ключ адреса</param>
    /// <param name="cancellationToken">Отмена чтения входящего тела</param>
    /// <returns>Идентификатор зафиксированного запуска</returns>
    [HttpPost(RiskRoutes.ById)]
    [RequestSizeLimit(MaxPayloadBytes)]
    [ProducesResponseType<ReviewWebhookAccepted>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<ReviewWebhookAccepted>> ReceiveAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!Request.IsHttps)
        {
            return BadRequest();
        }
        if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization.ToString(), out var authorization)
            || !string.Equals(authorization.Scheme, BearerScheme, StringComparison.OrdinalIgnoreCase)
            || authorization.Parameter is not { Length: ReviewRules.TokenLength } token)
        {
            return Unauthorized();
        }

        await useCases.AuthorizeReviewWebhookAsync(id, token);
        using var payload = new MemoryStream();
        try
        {
            await Request.Body.CopyToAsync(payload, cancellationToken);
        }
        catch (BadHttpRequestException error) when (error.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return Problem(statusCode: StatusCodes.Status413PayloadTooLarge);
        }
        var receipt = await useCases.AcceptReviewWebhookAsync(id, token, payload.ToArray());
        return StatusCode(StatusCodes.Status202Accepted, receipt);
    }
}
