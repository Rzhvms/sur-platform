using Api.Constants;
using Asp.Versioning;
using Api.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Health;

/// <summary>
/// Проверка рабочей схемы выбранного клиента
/// </summary>
/// <param name="useCases">Проверка готовности клиентской базы</param>
[ApiController]
[ApiVersionNeutral]
[AllowAnonymous]
[Route(IdentityRoutes.Health)]
public sealed class HealthController(SystemUseCaseManager useCases) : ControllerBase
{
    /// <summary>
    /// Проверяет готовность базы для указанного HTTPS-хоста
    /// </summary>
    /// <param name="cancellationToken">Отмена HTTP-запроса</param>
    /// <returns>200 для готовой базы или 503, если запросы обслуживать рано</returns>
    [HttpGet(IdentityRoutes.Ready)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ReadyAsync(CancellationToken cancellationToken)
    {
        if (await useCases.IsDatabaseReadyAsync(cancellationToken))
        {
            return Ok();
        }

        return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: IdentityHttp.DatabaseNotReady);
    }
}
