namespace Application.Ports.Services;

/// <summary>
/// Технические метаданные выбранного клиента для системных сценариев
/// </summary>
public interface ISystemMetadataService
{
    /// <summary>
    /// Проверяет готовность схемы базы выбранного клиента
    /// </summary>
    Task<bool> IsDatabaseReadyAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Читает канонический issuer выбранного клиента
    /// </summary>
    string GetIssuer();

    /// <summary>
    /// Читает открытые компоненты ключа проверки JWT
    /// </summary>
    (string Module, string Exponent) GetSigningKey();
}