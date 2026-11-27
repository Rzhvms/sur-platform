using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Application.Helpers.Encryption;

/// <summary>
/// Вспомогательный сервис шифрования паролей
/// </summary>
public static class PasswordEncryptionHelper
{
    #region Constants

    // Константы для заумного хэширования пароля

    private const int SaltSize = 16;
    private const int HashSize = 32;

    private const int Iterations = 2;
    private const int MemorySize = 19456;
    private const int Parallelism = 1;

    private const string Argon2Id = "argon2id";
    private const string ColonMark = ":";

    #endregion

    /// <summary>
    /// Генерация дополнительной hash-строки для пароля
    /// </summary>
    public static byte[] GenerateSalt()
    {
        return RandomNumberGenerator.GetBytes(SaltSize);
    }

    /// <summary>
    /// Хэширование пароля
    /// Принцип: algorithm:iterations:memory:parallelism:salt:hash
    /// </summary>
    public static string HashPassword(string password, byte[] hashSalt)
    {
        ArgumentNullException.ThrowIfNull(password);

        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = hashSalt,
            Iterations = Iterations,
            MemorySize = MemorySize,
            DegreeOfParallelism = Parallelism
        };

        var hash = argon2.GetBytes(HashSize);

        var someDifficultRow = string.Join(
            ColonMark, Argon2Id, Iterations,
            MemorySize, Parallelism,
            Convert.ToBase64String(hashSalt),
            Convert.ToBase64String(hash));

        return someDifficultRow;
    }

    /// <summary>
    /// Проверка пароля относительно сохраненной строки-хэша
    /// </summary>
    public static bool VerifyPassword(string password, string storedHash)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(storedHash);

        var parts = storedHash.Split(ColonMark);
        if (parts.Length != 6 || !parts[0].Equals(Argon2Id, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations) ||
            !int.TryParse(parts[2], out var memorySize) ||
            !int.TryParse(parts[3], out var parallelism))
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;

        try
        {
            salt = Convert.FromBase64String(parts[4]);
            expectedHash = Convert.FromBase64String(parts[5]);
        }
        catch
        {
            return false;
        }

        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            Iterations = iterations,
            MemorySize = memorySize,
            DegreeOfParallelism = parallelism
        };

        var actualHash = argon2.GetBytes(expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
