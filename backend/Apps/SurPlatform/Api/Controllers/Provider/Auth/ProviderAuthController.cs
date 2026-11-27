using Api.Constants;
using Core.Api.Controllers.ControllerTypes;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Provider.Auth;

/// <summary>
/// Административные операции создания пользователей
/// </summary>
[Route(IdentityRoutes.Connect)]
public class ProviderAuthController : BaseProviderController
{
    /// <summary>
    /// Register route
    /// </summary>
    private const string RegisterRoute = "register";

    /// <summary>
    /// Registration replacement
    /// </summary>
    private const string RegistrationReplacement = "Используйте POST /api/v1/provider/users";

    /// <summary>
    /// Регистрация пользователя
    /// </summary>
    [HttpPost(RegisterRoute)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public IActionResult Register()
    {
        return Problem(statusCode: StatusCodes.Status410Gone, title: RegistrationReplacement);
    }
}
