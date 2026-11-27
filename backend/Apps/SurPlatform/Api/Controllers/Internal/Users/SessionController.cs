using Application.UseCases.Auth.Dto.Request;
using Application.UseCases.Auth.interfaces;
using Core.Api.Controllers.ControllerTypes;
using Core.Api.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Internal.Users;

/// <summary>
/// Служебная проверка сессии и права в выбранной клиентской базе
/// </summary>
[Route("sessions")]
[Authorize(AuthenticationSchemes = SystemRequestHttpHandler.AuthenticationScheme)]
public sealed class SessionController(IAuthUseCaseManager useCaseManager) : BaseInternalController
{
    /// <summary>
    /// Проверяет актуальность сессии и наличие системного права
    /// </summary>
    [HttpPost("check")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CheckAsync(CheckSessionRequest request, CancellationToken cancellationToken)
    {
        var result = await useCaseManager.CheckSessionRightAsync(request);
        return !result ? StatusCode(StatusCodes.Status403Forbidden) : NoContent();
    }
}