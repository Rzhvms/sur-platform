using Application.UseCases.Treatments;
using Application.UseCases.Jira;
using Application.UseCases.Risks;
using Application.UseCases.Organization;
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
public static class ApplicationProviderBuilderExtensions
{
    /// <summary>
    /// Добавление зависимостей
    /// </summary>
    public static void AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AccountService>();
        services.AddScoped<IdentityInvitationService>();
        services.AddScoped<SessionService>();

        services.AddScoped<IAuthUseCaseManager, AuthUseCaseManager>();
        services.AddScoped<IdentityAdministrationUseCaseManager>();
        services.AddScoped<OrganizationUseCaseManager>();
        services.AddScoped<RiskUseCaseManager>();
        services.AddScoped<JiraUseCaseManager>();
        services.AddScoped<ReviewUseCaseManager>();
        services.AddScoped<TreatmentUseCaseManager>();
        services.AddScoped<IUserUseCaseManager, UserUseCaseManager>();
    }
}
