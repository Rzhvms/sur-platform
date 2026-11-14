using Api.Constants;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Health;

/// <summary>Проверка жизни процесса без обращения к клиентским базам и интеграциям</summary>
[ApiController]
[ApiVersionNeutral]
[AllowAnonymous]
[Route(IdentityRoutes.Health)]
public sealed class LivenessController : ControllerBase
{
    /// <summary>Подтверждает, что веб-хост принимает запросы</summary>
    /// <returns>HTTP 200 без данных окружения</returns>
    [HttpGet(IdentityRoutes.Live)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Live()
    {
        return Ok();
    }
}
