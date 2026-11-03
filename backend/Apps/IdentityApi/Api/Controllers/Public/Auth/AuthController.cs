using Application.UseCases.Auth.Dto.Request;
using Application.UseCases.Auth.Dto.Response.ConnectToken;
using Application.UseCases.Auth.Dto.Response.RefreshToken;
using Application.UseCases.Auth.Dto.Response.RevocateToken;
using Application.UseCases.Auth.interfaces;
using Application.Constants;
using Core.Api.Extensions;
using Core.Api.Controllers.ControllerTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Public.Auth;

/// <summary>
/// Клиентские операции авторизации и управления токенами
/// </summary>
[Route("connect")]
public class AuthController(IAuthUseCaseManager authUseCaseManager) : BasePublicController
{
    /// <summary>
    /// Авторизация пользователя
    /// </summary>
    [AllowAnonymous]
    [HttpPost("token")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConnectTokenAsync(ConnectTokenRequest request)
    {
        var response = await authUseCaseManager.ConnectTokenAsync(request);
        return response is ConnectTokenErrorResponse error ? BadRequest(error) : Ok(response);
    }
    
    /// <summary>
    /// Выход пользователя из системы. Отзыв refresh токена
    /// </summary>
    [Authorize]
    [HttpPost("token/revoke")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RevocateRefreshTokenAsync()
    {
        var userId = User.GetUserId();

        if (!Guid.TryParse(User.FindFirst(AccessTokenClaimTypes.SessionId)?.Value, out var sessionId) || sessionId == Guid.Empty)
        {
            return Unauthorized();
        }

        var response = await authUseCaseManager.RevocateRefreshTokenAsync(userId, sessionId);
        return response is RevocateTokenErrorResponse error ? BadRequest(error) : Ok(response);
    }
    
    /// <summary>
    /// Обновление Refresh токена
    /// </summary>
    [AllowAnonymous]
    [HttpPost("token/refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var response = await authUseCaseManager.RefreshTokenAsync(request);
        return response is RefreshTokenErrorResponse error ? BadRequest(error) : Ok(response);
    }
    
    /// <summary>
    /// Изменение пароля
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task ChangePasswordAsync(ChangePasswordRequest request)
    {
        var userId = User.GetUserId();
        await authUseCaseManager.ChangePasswordAsync(userId, request);
    }
}
