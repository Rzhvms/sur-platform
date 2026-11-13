using Core.Abstractions.Exceptions.Base;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Core.Api.Middlewares;

/// <summary>
/// Отлавливаем кастомные ошибки
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    ///
    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invoke
    /// </summary>
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError("Ошибка запроса {TraceId}: {ErrorType}", context.TraceIdentifier, ex.GetType().Name);
            if (context.Response.HasStarted)
            {
                throw;
            }

            var statusCode = ex is BaseException ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError;

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            await context.Response.WriteAsJsonAsync(new
            {
                error = ex is BaseException ? ex.Message : "Внутренняя ошибка сервера",
                type = ex is BaseException ? ex.GetType().Name : "InternalServerError",
                traceId = context.TraceIdentifier
            });
        }
    }
}