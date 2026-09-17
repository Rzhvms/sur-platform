using Microsoft.Extensions.DependencyInjection;

namespace IdentityApi.Application;

public static class ApplicationStartup
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}