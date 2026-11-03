using Application.Settings;
using Infrastructure.Services.Jwt.RsaKeys.Interfaces;
using Infrastructure.Services.Jwt.RsaKeys.Services;
using Infrastructure.Services.Jwt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Services.Jwt;

public static class JwtProviderBuilderExtensions
{
    /// <summary>
    /// Добавление зависимостей
    /// </summary>
    public static IServiceCollection AddIdentityJwt(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<JwtSettings>()
            .Bind(configuration.GetSection("Jwt"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<JwtKeyStoreSettings>()
            .Bind(configuration.GetSection("JwtKeys"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IRsaKeyProvider, FileSystemRsaKeyProvider>();
        services.AddSingleton(sp => sp.GetRequiredService<IRsaKeyProvider>().SigningKey);
        services.AddSingleton(sp => sp.GetRequiredService<IRsaKeyProvider>().ValidationKey);
        
        return services;
    }
}