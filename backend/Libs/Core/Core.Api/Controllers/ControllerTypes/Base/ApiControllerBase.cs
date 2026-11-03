using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Core.Api.Controllers.ControllerTypes.Base;

/// <summary>
/// Базовый контроллер
/// </summary>
[ApiController]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
public abstract class ApiControllerBase : ControllerBase
{
}