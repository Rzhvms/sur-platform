using Application.UseCases.Auth;
using Application.UseCases.Auth.interfaces;
using Application.UseCases.Users;
using Application.UseCases.Users.Interfaces;
using Application.Services.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

/// <summary>
/// Регистрация зависимостей уровня Application
/// </summary>
public static class ApplicationStartUp
{
    /// <summary>
    /// Добавление зависимостей
    /// </summary>
    public static void AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AccountService>();
        services.AddScoped<SessionService>();

        services.AddScoped<IAuthUseCaseManager, AuthUseCaseManager>();
        services.AddScoped<IUserUseCaseManager, UserUseCaseManager>();
    }
}