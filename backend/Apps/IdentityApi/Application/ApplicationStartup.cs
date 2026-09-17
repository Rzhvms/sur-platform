using Microsoft.Extensions.DependencyInjection;

namespace IdentityApi.Application;

/// <summary>
/// Регистрация зависимостей уровня Application
/// </summary>
public static class ApplicationStartup
{
    /// <summary>
    /// Добавление зависимостей
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}