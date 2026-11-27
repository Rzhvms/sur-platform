namespace Api.Controllers.Public.Auth.Dto.Response;

/// <summary>
/// Access-токен для памяти браузера без исходного refresh-токена
/// </summary>
/// <param name="AccessToken">Подписанный токен текущей браузерной сессии</param>
public sealed record BrowserAccessResponse(string AccessToken);
