using Core.Api.Controllers.ControllerTypes.Attributes;
using Core.Api.Controllers.ControllerTypes.Base;
using Core.Api.Controllers.ControllerTypes.Constants;

namespace Core.Api.Controllers.ControllerTypes;

/// <summary>
/// Базовый контроллер запросов клиентского приложения
/// </summary>
[PublicController]
[ControllerPrefix(ControllerPrefix.PublicRoutePrefix)]
public abstract class BasePublicController : ApiControllerBase
{
}
