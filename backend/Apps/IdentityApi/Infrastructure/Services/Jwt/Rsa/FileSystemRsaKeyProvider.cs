using System.Security.Cryptography;
using IdentityApi.Infrastructure.Services.Jwt.Rsa.Interfaces;
using IdentityApi.Infrastructure.Services.Jwt.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IdentityApi.Infrastructure.Services.Jwt.Rsa;

/// <summary>
/// Файловая реализация провайдера RSA-ключей
/// </summary>
internal sealed class FileSystemRsaKeyProvider : IRsaKeyProvider
{
    /// <summary>
    /// Приватный ключ
    /// </summary>
    private readonly RSA _signingRsa;
    
    /// <summary>
    /// Публичный ключ
    /// </summary>
    private readonly RSA _validationRsa;

    /// <inheritdoc />
    public RsaSecurityKey SigningKey { get; }
    
    /// <inheritdoc />
    public RsaSecurityKey ValidationKey { get; }

    ///
    public FileSystemRsaKeyProvider(IOptions<JwtKeyStoreSettings> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        
        var settings = options.Value;
        
        ValidateFileName(settings.PrivateKeyFileName, nameof(settings.PrivateKeyFileName));
        ValidateFileName(settings.PublicKeyFileName, nameof(settings.PublicKeyFileName));

        var directoryPath = Path.IsPathFullyQualified(settings.DirectoryPath)
            ? settings.DirectoryPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, settings.DirectoryPath));
        
        Directory.CreateDirectory(directoryPath);

        var privateKeyPath = Path.Combine(directoryPath, settings.PrivateKeyFileName);
        var publicKeyPath = Path.Combine(directoryPath, settings.PublicKeyFileName);
        
        EnsurePrivateKeyExists(privateKeyPath, settings.RsaKeySize);

        _signingRsa = RSA.Create();
        try
        {
            _signingRsa.ImportFromPem(File.ReadAllText(privateKeyPath));
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException)
        {
            _signingRsa.Dispose();
            throw new InvalidOperationException($"Не удалось прочитать приватный RSA-ключ '{privateKeyPath}'", exception);
        }

        if (_signingRsa.KeySize < 2048)
        {
            _signingRsa.Dispose();
            throw new InvalidOperationException("RSA-ключ короче 2048 бит небезопасен для подписи JWT");
        }

        var publicPem = _signingRsa.ExportSubjectPublicKeyInfoPem();
        SynchronizePublicKey(publicKeyPath, publicPem);

        _validationRsa = RSA.Create();
        _validationRsa.ImportFromPem(publicPem);

        var keyId = CreateKeyId(_validationRsa.ExportSubjectPublicKeyInfo());
        
        SigningKey = new RsaSecurityKey(_signingRsa)
        {
            KeyId = keyId
        };
        
        ValidationKey = new RsaSecurityKey(_validationRsa)
        {
            KeyId = keyId
        };
    }

    /// <summary>
    /// Создание и сохранение приватного ключа, если он отсутствует
    /// </summary>
    private static void EnsurePrivateKeyExists(string privateKeyPath, int keySize)
    {
        if (File.Exists(privateKeyPath))
        {
            return;
        }

        using var rsa = RSA.Create(keySize);
        var temporaryPath = $"{privateKeyPath}.{Guid.NewGuid():N}.tmp";
        
        try
        {
            File.WriteAllText(temporaryPath, rsa.ExportPkcs8PrivateKeyPem());
            RestrictPrivateKeyPermissions(temporaryPath);

            try
            {
                // Move без overwrite не позволит двум экземплярам сервиса затереть ключ друг друга
                File.Move(temporaryPath, privateKeyPath, overwrite: false);
            }
            catch (IOException) when (File.Exists(privateKeyPath))
            {
                // Если другой экземпляр успел создать ключ первым - используем его
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>
    /// Синхронизация публичного ключа с приватным
    /// </summary>
    private static void SynchronizePublicKey(string publicKeyPath, string publicPem)
    {
        if (File.Exists(publicKeyPath) &&
            string.Equals(File.ReadAllText(publicKeyPath), publicPem, StringComparison.Ordinal))
        {
            return;
        }

        var temporaryPath = $"{publicKeyPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, publicPem);
            File.Move(temporaryPath, publicKeyPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>
    /// Формирует идентификатор RSA-ключа на основании его публичной части
    /// </summary>
    private static string CreateKeyId(byte[] publicKey)
    {
        var fingerprint = SHA256.HashData(publicKey);
        return Base64UrlEncoder.Encode(fingerprint);
    }

    /// <summary>
    /// Проверка, что значение содержит только имя файла без пути
    /// </summary>
    private static void ValidateFileName(string fileName, string parameterName)
    {
        if (Path.IsPathFullyQualified(fileName) || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Параметр {parameterName} должен содержать только имя файла");
        }
    }

    /// <summary>
    /// Ограничение прав доступа к приватному ключу текущим пользователем
    /// </summary>
    private static void RestrictPrivateKeyPermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    /// <inheritdoc /> 
    public void Dispose()
    {
        _signingRsa.Dispose();
        _validationRsa.Dispose();
    }
}