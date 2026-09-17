namespace IdentityApi.Infrastructure.Services.Encryption.Models;

/// <summary>
/// Разобранные параметры и данные сохраненного Argon2id-хеша
/// </summary>
public readonly record struct ParsedPasswordHash(int Iterations, int MemorySize, int Parallelism, byte[] Salt, byte[] Hash);