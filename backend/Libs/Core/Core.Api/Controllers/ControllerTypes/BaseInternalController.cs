using Core.Api.Controllers.ControllerTypes.Attributes;
using Core.Api.Controllers.ControllerTypes.Base;
using Core.Api.Controllers.ControllerTypes.Constants;

namespace Core.Api.Controllers.ControllerTypes;

/// <summary>
/// Базовый контроллер внутренних запросов
/// </summary>
[InternalController]
[ControllerPrefix(ControllerPrefix.InternalRoutePrefix)]
public abstract class BaseInternalController : ApiControllerBase
{
}