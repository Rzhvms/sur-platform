using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using IdentityApi.Application.Encryption.Interfaces;
using IdentityApi.Infrastructure.Encryption.Models;
using Konscious.Security.Cryptography;

namespace IdentityApi.Infrastructure.Encryption;

/// <inheritdoc/>
internal sealed class PasswordEncryptionService : IPasswordEncryptionService
{
    /// <summary>
    /// Размер соли в байтах
    /// </summary>
    private const int SaltSize = 16;
    
    /// <summary>
    /// Размер результирующего хеша в байтах
    /// </summary>
    private const int HashSize = 32;
    
    /// <summary>
    /// Количество итераций Argon2id
    /// </summary>
    private const int Iterations = 2;
    
    /// <summary>
    /// Объем памяти Argon2id в KiB
    /// </summary>
    private const int MemorySize = 19_456;
    
    /// <summary>
    /// Степень параллелизма Argon2id
    /// </summary>
    private const int Parallelism = 1;

    /// <summary>
    /// Максимально допустимое количество итераций при проверке сохраненного хеша
    /// </summary>
    private const int MaxIterations = 10;
    
    /// <summary>
    /// Максимально допустимый объем памяти Argon2id в KiB при проверке сохраненного хеша
    /// </summary>
    private const int MaxMemorySize = 262_144;
    
    /// <summary>
    /// Максимально допустимая степень параллелизма при проверке сохраненного хеша
    /// </summary>
    private const int MaxParallelism = 16;
    
    /// <summary>
    /// Максимально допустимый размер соли в байтах
    /// </summary>
    private const int MaxSaltSize = 64;
    
    /// <summary>
    /// Максимально допустимый размер хеша в байтах
    /// </summary>
    private const int MaxHashSize = 64;

    /// <inheritdoc />
    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = DeriveHash(password, salt, Iterations, MemorySize, Parallelism, HashSize);

        return string.Join(
            ':',
            "argon2id",
            Iterations.ToString(CultureInfo.InvariantCulture),
            MemorySize.ToString(CultureInfo.InvariantCulture),
            Parallelism.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    /// <inheritdoc />
    public bool VerifyPassword(string password, string storedHash)
    {
        ArgumentNullException.ThrowIfNull(password);

        if (!TryParseHash(storedHash, out var parsedHash))
        {
            return false;
        }

        var actualHash = DeriveHash(
            password,
            parsedHash.Salt,
            parsedHash.Iterations,
            parsedHash.MemorySize,
            parsedHash.Parallelism,
            parsedHash.Hash.Length);

        try
        {
            return CryptographicOperations.FixedTimeEquals(actualHash, parsedHash.Hash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actualHash);
        }
    }

    /// <inheritdoc />
    public bool NeedsRehash(string storedHash)
    {
        return !TryParseHash(storedHash, out var parsedHash) ||
               parsedHash.Iterations != Iterations ||
               parsedHash.MemorySize != MemorySize ||
               parsedHash.Parallelism != Parallelism ||
               parsedHash.Salt.Length != SaltSize ||
               parsedHash.Hash.Length != HashSize;
    }

    /// <summary>
    /// Вычисляет Argon2id-хеш пароля с указанными параметрами
    /// </summary>
    private static byte[] DeriveHash(string password, byte[] salt, int iterations, int memorySize, int parallelism, int hashSize)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                Iterations = iterations,
                MemorySize = memorySize,
                DegreeOfParallelism = parallelism
            };

            return argon2.GetBytes(hashSize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    /// <summary>
    /// Разбирает сохраненный Argon2id-хеш и проверяет допустимость его параметров
    /// </summary>
    private static bool TryParseHash(string? storedHash, out ParsedPasswordHash parsedHash)
    {
        parsedHash = default;
        
        if (string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var parts = storedHash.Split(':');
        
        if (parts.Length != 6 || !parts[0].Equals("argon2id", StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var memorySize) ||
            !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var parallelism) ||
            iterations is < 1 or > MaxIterations ||
            memorySize is < 8 or > MaxMemorySize ||
            parallelism is < 1 or > MaxParallelism)
        {
            return false;
        }

        if (memorySize < 8 * parallelism)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[4]);
            var hash = Convert.FromBase64String(parts[5]);
            
            if (salt.Length is < 8 or > MaxSaltSize || hash.Length is < 16 or > MaxHashSize)
            {
                return false;
            }

            parsedHash = new ParsedPasswordHash(iterations, memorySize, parallelism, salt, hash);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
