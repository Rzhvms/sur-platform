using Application.UseCases.Users.Dto.Response;
using Application.UseCases.Users.Interfaces;
using Core.Api.Controllers.ControllerTypes;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Provider.Users;

/// <summary>
/// Административные операции управления пользователями
/// </summary>
[Route("users")]
public class ProviderUserController(IUserUseCaseManager useCaseManager) : BaseProviderController
{
    /// <summary>
    /// Получить список пользователей
    /// </summary>
    [HttpGet("list")]
    [ProducesResponseType(typeof(GetUserListResponse), StatusCodes.Status200OK)]
    public async Task<GetUserListResponse> GetUserListAsync()
    {
        return await useCaseManager.GetUserListAsync();
    }

    /// <summary>
    /// Получить пользователя по идентификатору
    /// </summary>
    [HttpGet("{userId}")]
    [ProducesResponseType(typeof(GetUserResponse), StatusCodes.Status200OK)]
    public async Task<GetUserResponse> GetUserByIdAsync([FromRoute] Guid userId)
    {
        return await useCaseManager.GetUserByIdAsync(userId);
    }

    /// <summary>
    /// Удалить пользователя по идентификатору
    /// </summary>
    [HttpDelete("{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task DeleteUserByIdAsync([FromRoute] Guid userId)
    {
        await useCaseManager.DeleteUserByIdAsync(userId);
    }
}