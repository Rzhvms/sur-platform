using Api.Constants;
using Api.Attributes;
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
[Route(IdentityRoutes.Connect)]
public class AuthController(IAuthUseCaseManager authUseCaseManager) : BasePublicController
{
    /// <summary>
    /// Password change replacement
    /// </summary>
    private const string PasswordChangeReplacement = "Используйте PATCH /api/v1/public/users/change-password";

    /// <summary>
    /// Авторизация пользователя
    /// </summary>
    [AllowAnonymous]
    [HttpPost(IdentityRoutes.Token)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConnectTokenAsync(ConnectTokenRequest request)
    {
        Response.Headers.CacheControl = IdentityHttp.NoStore;
        var response = await authUseCaseManager.ConnectTokenAsync(request);
        return response is ConnectTokenErrorResponse error ? BadRequest(error) : Ok(response);
    }

    /// <summary>
    /// Выход пользователя из системы. Отзыв refresh токена
    /// </summary>
    [Authorize]
    [HttpPost(IdentityRoutes.RevokeToken)]
    [AllowPasswordChangeSession]
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
    [HttpPost(IdentityRoutes.RefreshToken)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshTokenAsync(RefreshTokenRequest request)
    {
        Response.Headers.CacheControl = IdentityHttp.NoStore;
        var response = await authUseCaseManager.RefreshTokenAsync(request);
        return response is RefreshTokenErrorResponse error ? BadRequest(error) : Ok(response);
    }

    /// <summary>
    /// Возвращает 410 для смены без проверки текущего пароля
    /// </summary>
    /// <returns>Сообщение с актуальным маршрутом</returns>
    [Authorize]
    [HttpPost(IdentityRoutes.ChangePassword)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public IActionResult ChangePassword()
    {
        return Problem(statusCode: StatusCodes.Status410Gone,
        title: PasswordChangeReplacement);
    }
}
