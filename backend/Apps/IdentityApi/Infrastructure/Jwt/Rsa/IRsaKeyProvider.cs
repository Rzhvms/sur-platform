using Microsoft.IdentityModel.Tokens;

namespace IdentityApi.Infrastructure.Jwt.Rsa;

/// <summary>
/// RSA-ключи для подписи и проверки jwt
/// </summary>
public interface IRsaKeyProvider : IDisposable
{
    /// <summary>
    /// Ключ для подписи токенов
    /// </summary>
    RsaSecurityKey SigningKey { get; }

    /// <summary>
    /// Ключ для проверки токенов
    /// </summary>
    RsaSecurityKey ValidationKey { get; }
}
