using System.ComponentModel.DataAnnotations;

namespace IdentityApi.Infrastructure.Jwt.Settings;

/// <summary>
/// Настройки файлового хранилища RSA-ключей
/// </summary>
public sealed record JwtKeyStoreSettings
{
    /// <summary>
    /// Имя секции конфигурации хранилища RSA-ключей
    /// </summary>
    public const string SectionName = "JwtKeys";

    /// <summary>
    /// Путь к директории хранения RSA-ключей
    /// </summary>
    [Required, MinLength(1)]
    public string DirectoryPath { get; init; } = "identity-keys";

    /// <summary>
    /// Имя файла приватного RSA-ключа
    /// </summary>
    [Required, MinLength(1)]
    public string PrivateKeyFileName { get; init; } = "jwt-private.pem";

    /// <summary>
    /// Имя файла публичного RSA-ключа
    /// </summary>
    [Required, MinLength(1)]
    public string PublicKeyFileName { get; init; } = "jwt-public.pem";

    /// <summary>
    /// Размер RSA-ключа в битах
    /// </summary>
    [Range(2048, 8192)]
    public int RsaKeySize { get; init; } = 3072;
}