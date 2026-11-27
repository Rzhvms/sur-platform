using Core.Api.Controllers;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Api;

public static class ApiProviderBuilderExtensions
{
    /// <summary>
    /// Добавление зависимостей
    /// </summary>
    public static IServiceCollection AddCoreApi(this IServiceCollection services)
    {
        services.AddCoreControllers();
        return services;
    }
}