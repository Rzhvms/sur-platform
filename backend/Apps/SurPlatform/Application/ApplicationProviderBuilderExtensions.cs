using Application.Services.Authentication.Interfaces;
using Application.UseCases.Treatments.Interfaces;
using Application.UseCases.Documents.Interfaces;
using Application.UseCases.Jira.Interfaces;
using Application.UseCases.Risks.Interfaces;
using Application.UseCases.Organization.Interfaces;
using Application.UseCases.Treatments;
using Application.UseCases.Jira;
using Application.UseCases.Documents;
using Application.UseCases.Risks;
using Application.UseCases.Organization;
using Application.UseCases.Auth;
using Application.UseCases.Auth.interfaces;
using Application.UseCases.Users;
using Application.UseCases.Users.Interfaces;
using Application.Services.Authentication;
using Application.UseCases.System;
using Application.UseCases.System.Interfaces;
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
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IIdentityAccessService, IdentityAccessService>();
        services.AddScoped<ISessionService, SessionService>();

        services.AddScoped<IAuthUseCaseManager, AuthUseCaseManager>();
        services.AddScoped<IIdentityAdministrationUseCaseManager, IdentityAdministrationUseCaseManager>();
        services.AddScoped<IOrganizationUseCaseManager, OrganizationUseCaseManager>();
        services.AddScoped<IRiskUseCaseManager, RiskUseCaseManager>();
        services.AddScoped<IJiraUseCaseManager, JiraUseCaseManager>();
        services.AddScoped<IDocumentUseCaseManager, DocumentUseCaseManager>();
        services.AddScoped<IReviewUseCaseManager, ReviewUseCaseManager>();
        services.AddScoped<ITreatmentUseCaseManager, TreatmentUseCaseManager>();
        services.AddScoped<IUserUseCaseManager, UserUseCaseManager>();
        services.AddScoped<ISystemUseCaseManager, SystemUseCaseManager>();
    }
}
