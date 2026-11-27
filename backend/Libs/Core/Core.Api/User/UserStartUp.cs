using Core.Abstractions.User;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Api.User;

public static class UserStartUp
{
    public static void AddCoreUser(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
    }
}
