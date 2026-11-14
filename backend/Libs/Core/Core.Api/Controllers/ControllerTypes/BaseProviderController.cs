using Core.Api.Controllers.ControllerTypes.Attributes;
using Core.Api.Controllers.ControllerTypes.Base;
using Core.Api.Controllers.ControllerTypes.Constants;

namespace Core.Api.Controllers.ControllerTypes;

/// <summary>
/// Базовый контроллер административных запросов
/// </summary>
[ProviderController]
[ControllerPrefix(ControllerPrefix.ProviderRoutePrefix)]
public abstract class BaseProviderController : ApiControllerBase
{
}