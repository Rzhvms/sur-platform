using Api.Constants;
using Asp.Versioning;
using Application.UseCases.System.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Health;

/// <summary>
/// Проверка состояния сервиса
/// </summary>
[ApiController]
[ApiVersionNeutral]
[AllowAnonymous]
[Route("health")]
public sealed class HealthController(ISystemUseCaseManager useCaseManager) : ControllerBase
{
    /// <summary>
    /// Подтверждает, что веб-хост принимает запросы
    /// </summary>
    [HttpGet("live")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult CheckServiceHeatlh()
    {
        return Ok();
    }
    
    /// <summary>
    /// Проверяет готовность базы для указанного HTTPS-хоста
    /// </summary>
    [HttpGet("ready")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        if (await useCaseManager.IsDatabaseReadyAsync(cancellationToken))
        {
            return Ok();
        }

        return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: IdentityHttp.DatabaseNotReady);
    }
}
