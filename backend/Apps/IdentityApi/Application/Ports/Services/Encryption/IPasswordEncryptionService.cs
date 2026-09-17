namespace IdentityApi.Application.Ports.Services.Encryption;

/// <summary>
/// Сервис хеширования паролей
/// </summary>
public interface IPasswordEncryptionService
{
    /// <summary>
    /// Хеширует пароль и возвращает самодостаточную строку:
    /// algorithm:iterations:memory:parallelism:salt:hash
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Проверяет пароль относительно сохраненной строки-хэша
    /// </summary>
    bool VerifyPassword(string password, string storedHash);

    /// <summary>
    /// Показывает, следует ли пересчитать корректный хеш с актуальными параметрами
    /// </summary>
    bool NeedsRehash(string storedHash);
}