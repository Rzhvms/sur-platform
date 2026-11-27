using Api.UseCases;
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
[Route(WellKnownController.WellKnownRoute)]
public class WellKnownController(SystemUseCaseManager useCases) : BaseInternalController
{
    /// <summary>
    /// Well known route
    /// </summary>
    internal const string WellKnownRoute = ".well-known";

    /// <summary>
    /// Jwks route
    /// </summary>
    private const string JwksRoute = "jwks.json";

    /// <summary>
    /// Rsa key type
    /// </summary>
    private const string RsaKeyType = "RSA";

    /// <summary>
    /// Signature use
    /// </summary>
    private const string SignatureUse = "sig";

    /// <summary>
    /// Rsa sha256
    /// </summary>
    private const string RsaSha256 = "RS256";

    /// <summary>
    /// Main key id
    /// </summary>
    private const string MainKeyId = "main-key";

    /// <summary>
    /// Discovery route
    /// </summary>
    private const string DiscoveryRoute = "openid-configuration";

    /// <summary>
    /// Jwks address unavailable
    /// </summary>
    private const string JwksAddressUnavailable = "Не удалось сформировать адрес JWKS";

    /// <summary>
    /// Возвращает JSON Web Key Set — публичные ключи для проверки JWT.
    /// </summary>
    /// <remarks>
    /// Используется внешними сервисами (API Gateway) для валидации подписи JWT токенов без доступа к приватному ключу.
    /// </remarks>
    /// <returns>Набор RSA публичных ключей в формате JWKS</returns>
    [HttpGet(JwksRoute)]
    public IActionResult GetJwks()
    {
        var key = useCases.GetSigningKey();

        return Ok(new
        {
            keys = new[]
            {
                new
                {
                    kty = RsaKeyType,
                    use = SignatureUse,
                    alg = RsaSha256,
                    n = key.Modulus,
                    e = key.Exponent,
                    kid = MainKeyId
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
    [HttpGet(DiscoveryRoute)]
    public IActionResult GetConfig()
    {
        var issuer = useCases.GetIssuer();
        var jwksUri = Url.ActionLink(nameof(GetJwks))
            ?? throw new InvalidOperationException(JwksAddressUnavailable);

        return Ok(new
        {
            issuer = issuer,
            jwks_uri = jwksUri
        });
    }
}
