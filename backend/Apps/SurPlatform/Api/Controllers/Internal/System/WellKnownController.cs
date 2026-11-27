using Application.UseCases.System.Interfaces;
using Core.Api.Controllers.ControllerTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Internal.System;

/// <summary>
/// Контроллер для предоставления публичных метаданных Identity сервиса
/// </summary>
/// <remarks>
/// Используется для:
/// - валидации JWT токенов внешними системами (Gateway, клиенты)
/// - автоматического discovery конфигурации Identity сервиса
///
/// Эндпоинты соответствуют спецификации:
/// - RFC 7517 (JSON Web Key Set - JWKS)
/// - OpenID Connect Discovery
/// - RFC 8615 (.well-known URIs)
/// </remarks>
/// <param name="useCases">Чтение публичных метаданных JWT</param>
[AllowAnonymous]
[Route(".well-known")]
public class WellKnownController(ISystemUseCaseManager useCases) : BaseInternalController
{
    /// <summary>
    /// Возвращает JSON Web Key Set — публичные ключи для проверки JWT.
    /// </summary>
    /// <remarks>
    /// Используется внешними сервисами (API Gateway) для валидации подписи JWT токенов без доступа к приватному ключу.
    /// </remarks>
    /// <returns>Набор RSA публичных ключей в формате JWKS</returns>
    [HttpGet("jwks.json")]
    public IActionResult GetJwks()
    {
        var key = useCases.GetSigningKey();

        return Ok(new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "RS256",
                    n = key.Module,
                    e = key.Exponent,
                    kid = "main-key"
                }
            }
        });
    }

    /// <summary>
    /// OpenID Connect discovery endpoint
    /// </summary>
    /// <remarks>
    /// Позволяет клиентам автоматически определить:
    /// - issuer сервиса
    /// - JWKS endpoint
    /// - будущие OAuth/OIDC endpoints
    /// </remarks>
    /// <returns>OpenID configuration документ</returns>
    [HttpGet("openid-configuration")]
    public IActionResult GetConfig()
    {
        var issuer = useCases.GetIssuer();
        var jwksUri = Url.ActionLink(nameof(GetJwks)) ?? throw new InvalidOperationException("Не удалось сформировать адрес JWKS");

        return Ok(new
        {
            issuer, jwks_uri = jwksUri
        });
    }
}