using Infrastructure.Services.Encryption;
using Infrastructure.Services.Jwt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Services;

public static class IdentityServicesStartUp
{
    /// <summary>
    /// Добавление зависимостей
    /// </summary>
    public static void AddIdentityServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentityJwt(configuration);
        services.AddIdentityEncryption();
    }
}
