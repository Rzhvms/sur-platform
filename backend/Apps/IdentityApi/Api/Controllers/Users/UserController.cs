using Application.UseCases.Users.Dto.Request;
using Application.UseCases.Users.Dto.Response;
using Application.UseCases.Users.Interfaces;
using Core.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Users;

[ApiController]
[Route("user")]
[Authorize]
public class UserController(IUserUseCaseManager useCaseManager) : ControllerBase
{
    /// <summary>
    /// Получить список пользователей
    /// </summary>
    [HttpGet("list")]
    [Authorize(Policy = Core.Api.Controllers.ControllerTypes.Constants.ControllerPolicies.Provider)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<GetUserListResponse> GetUserListAsync()
    {
        return await useCaseManager.GetUserListAsync();
    }

    /// <summary>
    /// Получить пользователя по идентификатору
    /// </summary>
    [HttpGet("{userId}")]
    [Authorize(Policy = Core.Api.Controllers.ControllerTypes.Constants.ControllerPolicies.Provider)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<GetUserResponse> GetUserByIdAsync([FromRoute] Guid userId)
    {
        return await useCaseManager.GetUserByIdAsync(userId);
    }

    /// <summary>
    /// Удалить пользователя по идентификатору
    /// </summary>
    [HttpDelete("{userId}")]
    [Authorize(Policy = Core.Api.Controllers.ControllerTypes.Constants.ControllerPolicies.Provider)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task DeleteUserByIdAsync([FromRoute] Guid userId)
    {
        await useCaseManager.DeleteUserByIdAsync(userId);
    }

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
