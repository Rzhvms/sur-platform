using Application.Settings;
using Infrastructure.Services.Jwt.RsaKeys.Interfaces;
using Infrastructure.Services.Jwt.RsaKeys.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Services.Jwt;

public static class JwtProviderBuilderExtensions
{
    /// <summary>
    /// Jwt section
    /// </summary>
    private const string JwtSection = "Jwt";

    /// <summary>
    /// Jwt keys section
    /// </summary>
    private const string JwtKeysSection = "JwtKeys";

    /// <summary>
    /// Добавление зависимостей
    /// </summary>
    public static IServiceCollection AddIdentityJwt(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSection))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<JwtKeyStoreSettings>()
            .Bind(configuration.GetSection(JwtKeysSection))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IRsaKeyProvider, FileSystemRsaKeyProvider>();
        services.AddSingleton(sp => sp.GetRequiredService<IRsaKeyProvider>().SigningKey);

        return services;
    }
}
