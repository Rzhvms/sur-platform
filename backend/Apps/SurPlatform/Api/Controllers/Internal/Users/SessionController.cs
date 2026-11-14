using Api.Controllers.Internal.Users.Dto.Request;
using Application.UseCases.Auth.interfaces;
using Core.Api.Controllers.ControllerTypes;
using Core.Api.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Internal.Users;

/// <summary>
/// Служебная проверка сессии и права в выбранной клиентской базе
/// </summary>
/// <param name="auth">Прикладные проверки Identity</param>
[Route(SessionController.SessionRoute)]
[Authorize(AuthenticationSchemes = SystemRequestHttpHandler.AuthenticationScheme)]
public sealed class SessionController(IAuthUseCaseManager auth) : BaseInternalController
{
    /// <summary>
    /// Относительный маршрут служебных операций с сессиями
    /// </summary>
    internal const string SessionRoute = "sessions";

    /// <summary>
    /// Имя команды проверки доступа
    /// </summary>
    private const string CheckRoute = "check";

    /// <summary>
    /// Проверяет актуальность сессии и наличие системного права
    /// </summary>
    /// <param name="request">Сессия, версия доступа и право операции</param>
    /// <param name="cancellationToken">Отмена проверки</param>
    /// <returns>204 при разрешении, 401 без служебного ключа или 403 при отказе</returns>
    [HttpPost(CheckRoute)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CheckAsync(CheckSessionRequest request, CancellationToken cancellationToken)
    {
        if (!await auth.CheckSessionRightAsync(request.UserId, request.SessionId, request.AccessVersion,
                request.RightCode, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return NoContent();
    }
}
