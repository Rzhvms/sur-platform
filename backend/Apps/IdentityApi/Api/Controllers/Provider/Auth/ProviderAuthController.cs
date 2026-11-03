using Application.UseCases.Auth.Dto.Request;
using Application.UseCases.Auth.Dto.Response.CreateUser;
using Application.UseCases.Auth.interfaces;
using Core.Api.Controllers.ControllerTypes;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Provider.Auth;

/// <summary>
/// Административные операции создания пользователей
/// </summary>
[Route("connect")]
public class ProviderAuthController(IAuthUseCaseManager authUseCaseManager) : BaseProviderController
{
    /// <summary>
    /// Создание пользователя / регистрация
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<CreateUserResponse> RegisterAsync(CreateUserRequest request)
    {
        return await authUseCaseManager.CreateUserAsync(request);
    }
}
