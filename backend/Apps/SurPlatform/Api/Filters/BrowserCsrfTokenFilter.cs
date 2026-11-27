using Api.Controllers.Public.Auth.Dto.Response;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Api.Filters;

/// <summary>
/// Формирует HTTP-ответ с CSRF-токеном и устанавливает связанную cookie
/// </summary>
internal sealed class BrowserCsrfTokenFilter(IAntiforgery antiforgery) : IResourceFilter
{
    /// <inheritdoc/>
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var tokens = antiforgery.GetAndStoreTokens(context.HttpContext);
        context.Result = new OkObjectResult(new BrowserCsrfResponse(tokens.RequestToken));
    }

    /// <inheritdoc/>
    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}