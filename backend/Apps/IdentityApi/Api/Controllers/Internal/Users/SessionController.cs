using Core.Api.Controllers.ControllerTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Internal.Users;

/// <summary>
/// Внутренние операции отладки сессий
/// </summary>
[Route("sessions")]
[Authorize]
public class SessionController : BaseInternalController
{
}
