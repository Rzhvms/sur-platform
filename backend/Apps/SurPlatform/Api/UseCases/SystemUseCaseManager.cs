using Infrastructure.Clients;
using Infrastructure.Services.Jwt.RsaKeys.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace Api.UseCases;

/// <summary>
/// Читает технические метаданные веб-хоста и готовность базы выбранного клиента
/// </summary>
/// <param name="database">Контекст текущего клиентского запроса</param>
/// <param name="initializer">Сервис подготовки и проверки баз</param>
/// <param name="keyProvider">Источник публичной части ключа JWT</param>
public sealed class SystemUseCaseManager(
    ClientDatabaseContext database, ClientDatabaseInitializer initializer, IRsaKeyProvider keyProvider)
{
    /// <summary>
    /// Ошибка чтения публичного RSA-ключа
    /// </summary>
    private const string MissingPublicKey = "Отсутствует открытый RSA-ключ JWT";

    /// <summary>
    /// Проверяет, что рабочее подключение видит все требуемые миграции
    /// </summary>
    /// <param name="cancellationToken">Отмена запроса</param>
    /// <returns>Готовность схемы текущего клиента</returns>
    public Task<bool> IsDatabaseReadyAsync(CancellationToken cancellationToken)
    {
        return initializer.IsReadyAsync(database.Client, cancellationToken);
    }

    /// <summary>
    /// Возвращает issuer для конфигурации текущего хоста
    /// </summary>
    /// <returns>Канонический HTTPS-адрес клиента</returns>
    public string GetIssuer()
    {
        return database.Client.Issuer;
    }

    /// <summary>
    /// Возвращает модуль и экспоненту публичного ключа для JWKS
    /// </summary>
    /// <returns>Два значения в кодировке Base64URL</returns>
    /// <exception cref="InvalidOperationException">Провайдер не вернул открытый RSA-ключ</exception>
    public (string Modulus, string Exponent) GetSigningKey()
    {
        var parameters = keyProvider.ValidationKey.Rsa?.ExportParameters(false);
        if (parameters?.Modulus is not { } modulus || parameters?.Exponent is not { } exponent)
        {
            throw new InvalidOperationException(MissingPublicKey);
        }

        return (Base64UrlEncoder.Encode(modulus), Base64UrlEncoder.Encode(exponent));
    }
}
