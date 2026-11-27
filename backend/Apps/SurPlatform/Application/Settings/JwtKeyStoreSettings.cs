using System.ComponentModel.DataAnnotations;

namespace Application.Settings;

/// <summary>
/// Настройки хранения RSA-ключей
/// </summary>
public sealed class JwtKeyStoreSettings
{
    /// <summary>
    /// Private key file
    /// </summary>
    private const string PrivateKeyFile = "jwt-private.pem";

    /// <summary>
    /// Public key file
    /// </summary>
    private const string PublicKeyFile = "jwt-public.pem";

    /// <summary>
    /// Адрес хранения ключей в директории проекта
    /// </summary>
    [Required]
    public required string DirectoryPath { get; init; }

    /// <summary>
    /// Название файла приватного ключа
    /// </summary>
    public string PrivateKeyFileName { get; init; } = PrivateKeyFile;

    /// <summary>
    /// Название файла публичного ключа
    /// </summary>
    public string PublicKeyFileName { get; init; } = PublicKeyFile;

    /// <summary>
    /// Размер RSA-ключа в байтах
    /// </summary>
    [Range(2048, 8192)]
    public int RsaKeySize { get; init; } = 4096;
}
