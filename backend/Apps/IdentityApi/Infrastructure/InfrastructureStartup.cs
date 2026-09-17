using IdentityApi.Application.Encryption.Interfaces;
using IdentityApi.Application.Jwt.Generators.Interfaces;
using IdentityApi.Infrastructure.Encryption;
using IdentityApi.Infrastructure.Jwt;
using IdentityApi.Infrastructure.Jwt.Rsa;
using IdentityApi.Infrastructure.Jwt.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IdentityApi.Infrastructure;

/// <summary>
/// Регистрация зависимостей уровня Infrastructure
/// </summary>
public static class InfrastructureStartup
{
    /// <summary>
    /// Добавление зависимостей
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetRequiredSection(JwtSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<JwtKeyStoreSettings>()
            .Bind(configuration.GetRequiredSection(JwtKeyStoreSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IPasswordEncryptionService, PasswordEncryptionService>();
        services.AddSingleton<IRsaKeyProvider, FileSystemRsaKeyProvider>();
        services.AddSingleton<IJwtGenerationService, JwtGenerationService>();

        return services;
    }
}
