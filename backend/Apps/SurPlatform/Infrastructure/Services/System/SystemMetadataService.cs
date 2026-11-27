using Application.Ports.Services;
using Infrastructure.Clients;
using Infrastructure.Services.Jwt.RsaKeys.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Services.System;

/// <summary>
/// Получает системные метаданные из выбранного клиентского контекста и хранилища ключей
/// </summary>
internal sealed class SystemMetadataService(ClientDatabaseContext database, ClientDatabaseInitializer initializer, IRsaKeyProvider keyProvider)
    : ISystemMetadataService
{
    /// <summary>
    /// Причина отказа при отсутствии открытых компонентов ключа
    /// </summary>
    private const string SigningKeyUnavailable = "Отсутствует открытый RSA-ключ JWT";

    /// <inheritdoc/>
    public Task<bool> IsDatabaseReadyAsync(CancellationToken cancellationToken)
    {
        return initializer.IsReadyAsync(database.Client, cancellationToken);
    }

    /// <inheritdoc/>
    public string GetIssuer()
    {
        return database.Client.Issuer;
    }

    /// <inheritdoc/>
    public (string Module, string Exponent) GetSigningKey()
    {
        var parameters = keyProvider.ValidationKey.Rsa?.ExportParameters(false);
        if (parameters?.Modulus is not { } modulus || parameters?.Exponent is not { } exponent)
        {
            throw new InvalidOperationException(SigningKeyUnavailable);
        }

        return (Base64UrlEncoder.Encode(modulus), Base64UrlEncoder.Encode(exponent));
    }
}