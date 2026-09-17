using IdentityApi.Application.Ports.Services.Authentication;
using IdentityApi.Application.Ports.Services.Encryption;
using IdentityApi.Infrastructure.Services.Encryption;
using IdentityApi.Infrastructure.Services.Jwt;
using IdentityApi.Infrastructure.Services.Jwt.Rsa;
using IdentityApi.Infrastructure.Services.Jwt.Rsa.Interfaces;
using IdentityApi.Infrastructure.Services.Jwt.Settings;
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

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordEncryptionService, PasswordEncryptionService>();
        services.AddSingleton<IRsaKeyProvider, FileSystemRsaKeyProvider>();
        services.AddSingleton<ITokenService, TokenService>();

        return services;
    }
}
