using Application.Ports.Services.Jwt;
using Infrastructure.Services.Jwt.RsaKeys.Interfaces;
using Infrastructure.Services.Jwt.RsaKeys.Services;
using Infrastructure.Services.Jwt;
using Infrastructure.Services.Jwt.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Services.Jwt;

public static class JwtForStartUp
{
    /// <summary>
    /// Добавление зависимостей
    /// </summary>
    public static void AddIdentityJwt(this IServiceCollection services, IConfiguration configuration)
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
        services.AddSingleton<IJwtGenerationService, JwtGenerationService>();
    }
}
