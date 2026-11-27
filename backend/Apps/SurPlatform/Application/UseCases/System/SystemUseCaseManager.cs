using Application.Ports.Services;
using Application.UseCases.System.Interfaces;

namespace Application.UseCases.System;

/// <summary>Координирует получение технических метаданных текущего клиента</summary>
/// <param name="metadata">Доступ к готовности базы, issuer и открытому ключу без инфраструктурных зависимостей</param>
internal sealed class SystemUseCaseManager(ISystemMetadataService metadata) : ISystemUseCaseManager
{
    /// <inheritdoc/>
    public Task<bool> IsDatabaseReadyAsync(CancellationToken cancellationToken)
    {
        return metadata.IsDatabaseReadyAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public string GetIssuer()
    {
        return metadata.GetIssuer();
    }

    /// <inheritdoc/>
    public (string Module, string Exponent) GetSigningKey()
    {
        return metadata.GetSigningKey();
    }
}
