namespace Api.Controllers.Public.Auth.Dto.Response;

/// <summary>
/// Токен для проверки браузерных запросов
/// </summary>
/// <param name="RequestToken">Значение заголовка X-CSRF-TOKEN</param>
public sealed record BrowserCsrfResponse(string? RequestToken);
