using Application.Ports.Services.Encryption;
using Infrastructure.Services.Encryption;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Services.Encryption;

public static class EncryptionForStartUp
{
    /// <summary>
    /// Добавление зависимостей
    /// </summary>
    public static void AddIdentityEncryption(this IServiceCollection services)
    {
        services.AddScoped<IPasswordEncryptionService, PasswordEncryptionService>();
    }
}
