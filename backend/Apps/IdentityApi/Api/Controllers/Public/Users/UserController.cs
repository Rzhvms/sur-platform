using Application.UseCases.Users.Dto.Request;
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
[Route("users")]
[Authorize]
public class UserController(IUserUseCaseManager useCaseManager) : BasePublicController
{
    /// <summary>
    /// Изменить пароль.
    /// </summary>
    [HttpPatch("change-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ChangeUserPasswordResponse> ChangeUserPasswordAsync(ChangeUserPasswordRequest request)
    {
        var id = User.GetUserId();
        return await useCaseManager.ChangePasswordAsync(id, request);
    }
}
