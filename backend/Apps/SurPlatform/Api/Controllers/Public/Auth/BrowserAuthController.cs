using Api.Filters;
using Api.Constants;
using Api.Controllers.Public.Auth.Dto.Response;
using Application.Constants;
using Application.UseCases.Auth.Dto.Request;
using Application.UseCases.Auth.Dto.Response.ConnectToken;
using Application.UseCases.Auth.Dto.Response.RefreshToken;
using Application.UseCases.Auth.interfaces;
using Core.Api.Controllers.ControllerTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Public.Auth;

/// <summary>
/// Браузерная сессия с защищённой cookie и явным CSRF-токеном
/// </summary>
/// <param name="auth">Сценарии входа и обновления</param>
[AllowAnonymous]
[Route("connect/browser")]
public sealed class BrowserAuthController(IAuthUseCaseManager auth)
    : BasePublicController
{
    /// <summary>
    /// Host-only имя cookie refresh-токена
    /// </summary>
    private const string RefreshCookie = "__Host-sur-refresh";

    /// <summary>
    /// Возвращает токен для заголовка X-CSRF-TOKEN и устанавливает CSRF-cookie
    /// </summary>
    /// <returns>Токен, действующий для этой браузерной cookie</returns>
    [HttpGet("csrf")]
    [TypeFilter(typeof(BrowserCsrfTokenFilter))]
    [ProducesResponseType<BrowserCsrfResponse>(StatusCodes.Status200OK)]
    public IActionResult Csrf()
    {
        // Ресурсный фильтр формирует ответ до выполнения действия
        return NoContent();
    }

    /// <summary>
    /// Возвращает access-токен и устанавливает refresh-cookie
    /// </summary>
    /// <param name="request">Логин и пароль</param>
    /// <returns>Access-токен или отказ во входе</returns>
    [HttpPost("token")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType<BrowserAccessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginAsync(ConnectTokenRequest request)
    {
        if (await auth.ConnectTokenAsync(request) is not ConnectTokenSuccessResponse response)
        {
            return Unauthorized();
        }

        Response.Headers.CacheControl = IdentityHttp.NoStore;
        Response.Cookies.Append(RefreshCookie, response.RefreshToken, Cookie());
        return Ok(new BrowserAccessResponse(response.AccessToken));
    }

    /// <summary>
    /// Ротирует refresh-cookie в рамках текущей сессии
    /// </summary>
    /// <returns>Новый access-токен или отказ</returns>
    [HttpPost("token/refresh")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType<BrowserAccessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshAsync()
    {
        if (!Request.Cookies.TryGetValue(RefreshCookie, out var refreshToken) ||
            await auth.RefreshTokenAsync(new RefreshTokenRequest { RefreshToken = refreshToken, AccessToken = string.Empty })
                is not RefreshTokenSuccessResponse response)
        {
            Response.Cookies.Delete(RefreshCookie, Cookie());
            return Unauthorized();
        }

        Response.Headers.CacheControl = IdentityHttp.NoStore;
        Response.Cookies.Append(RefreshCookie, response.RefreshToken, Cookie());
        return Ok(new BrowserAccessResponse(response.AccessToken));
    }

    /// <summary>
    /// Завершает сессию по refresh-cookie и удаляет её из браузера
    /// </summary>
    /// <returns>204 после локального завершения</returns>
    [HttpPost("token/revoke")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAsync()
    {
        if (Request.Cookies.TryGetValue(RefreshCookie, out var refreshToken))
        {
            await auth.RevokeBrowserSessionAsync(refreshToken);
        }

        Response.Cookies.Delete(RefreshCookie, Cookie());
        return NoContent();
    }

    /// <summary>
    /// Атрибуты браузерной cookie без общего домена
    /// </summary>
    /// <returns>Параметры установки и удаления</returns>
    private static CookieOptions Cookie()
    {
        return new()
    {
        HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = IdentityHttp.RootPath
    };
    }
}
