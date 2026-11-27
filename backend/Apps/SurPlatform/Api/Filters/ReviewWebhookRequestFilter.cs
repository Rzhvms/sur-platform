using System.Net.Http.Headers;
using Api.Controllers.Public.Risks.Dto.Request;
using Application.Constants;
using Application.UseCases.Risks.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Api.Filters;

/// <summary>
/// Авторизует источник пересмотра до чтения ограниченного тела HTTP-запроса
/// </summary>
internal sealed class ReviewWebhookRequestFilter(IReviewUseCaseManager useCases, ProblemDetailsFactory problems) : IAsyncResourceFilter
{
    /// <summary>
    /// Схема отдельного секрета внешнего источника
    /// </summary>
    private const string BearerScheme = "Bearer";

    /// <summary>
    /// Имя параметра пути, содержащего ключ внешнего источника
    /// </summary>
    private const string RiskIdRouteValue = "id";

    /// <inheritdoc/>
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (!request.IsHttps)
        {
            context.Result = new BadRequestResult();
            return;
        }

        if (!AuthenticationHeaderValue.TryParse(request.Headers.Authorization.ToString(), out var authorization)
            || !string.Equals(authorization.Scheme, BearerScheme, StringComparison.OrdinalIgnoreCase)
            || authorization.Parameter is not { Length: ReviewRules.TokenLength } token)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!Guid.TryParse(Convert.ToString(context.RouteData.Values[RiskIdRouteValue], System.Globalization.CultureInfo.InvariantCulture), out var id))
        {
            context.Result = new BadRequestResult();
            return;
        }
        await useCases.AuthorizeReviewWebhookAsync(id, token);
        using var payload = new MemoryStream();
        try
        {
            await request.Body.CopyToAsync(payload, context.HttpContext.RequestAborted);
        }
        catch (BadHttpRequestException error) when (error.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            var problem = problems.CreateProblemDetails(context.HttpContext, StatusCodes.Status413PayloadTooLarge);
            context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status413PayloadTooLarge };
            return;
        }

        context.HttpContext.Items[typeof(ReviewWebhookRequest)] = new ReviewWebhookRequest { Token = token, Payload = payload.ToArray() };
        await next();
    }
}