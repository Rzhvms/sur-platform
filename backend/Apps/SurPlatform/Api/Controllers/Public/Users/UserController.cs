using Api.Constants;
using Api.Extensions;
using Application.UseCases.Users.Dto.Request;
using Api.Attributes;
using Application.UseCases.Users.Dto.Response;
using Application.UseCases.Users.Interfaces;
using Core.Api.Controllers.ControllerTypes;
using Core.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Public.Users;

/// <summary>
/// Клиентские операции текущего пользователя
/// </summary>
[Route(IdentityRoutes.Users)]
[Authorize]
public sealed class UserController(IUserUseCaseManager useCaseManager) : BasePublicController
{
    /// <summary>Маршрут профиля доступа текущего пользователя</summary>
    private const string CurrentUserRoute = "me";

    /// <summary>Возвращает стабильные коды прав текущей сессии</summary>
    /// <returns>Идентификатор пользователя и его эффективные права</returns>
    [HttpGet(CurrentUserRoute)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<CurrentUserAccessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<CurrentUserAccessResponse> GetCurrentAccessAsync()
    {
        return useCaseManager.GetCurrentAccessAsync(User.GetRiskActor());
    }

    /// <summary>
    /// Изменить пароль
    /// </summary>
    [HttpPatch(IdentityRoutes.ChangePassword)]
    [AllowPasswordChangeSession]
    [ProducesResponseType(typeof(ChangeUserPasswordResponse), StatusCodes.Status200OK)]
    public async Task<ChangeUserPasswordResponse> ChangeUserPasswordAsync(ChangeUserPasswordRequest request)
    {
        var id = User.GetUserId();
        return await useCaseManager.ChangePasswordAsync(id, request);
    }
}
