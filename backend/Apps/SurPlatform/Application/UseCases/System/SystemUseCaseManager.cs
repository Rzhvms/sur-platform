using Application.UseCases.System.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace Application.UseCases.System;

/// <inheritdoc/>
internal sealed class SystemUseCaseManager(ClientDatabaseContext database, ClientDatabaseInitializer initializer, IRsaKeyProvider keyProvider) : ISystemUseCaseManager
{
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
            throw new InvalidOperationException("Отсутствует открытый RSA-ключ JWT");
        }

        return (Base64UrlEncoder.Encode(modulus), Base64UrlEncoder.Encode(exponent));
    }
}