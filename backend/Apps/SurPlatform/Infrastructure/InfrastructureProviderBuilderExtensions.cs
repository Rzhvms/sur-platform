using Infrastructure.Services.Jira.Interfaces;
using Infrastructure.Services.Documents.Interfaces;
using Infrastructure.Services.Demo.Interfaces;
using Infrastructure.Services.Identity.Interfaces;
using Infrastructure.Repositories.Reviews;
using Infrastructure.Repositories.Documents;
using Infrastructure.Repositories.Jira;
using Infrastructure.Repositories.Treatments.Interfaces;
using Infrastructure.Repositories.Treatments;
using Infrastructure.Repositories.History;
using Infrastructure.Repositories.References;
using Infrastructure.Repositories.Assessments.Interfaces;
using Infrastructure.Repositories.Assessments;
using Infrastructure.Repositories.Reviews.Interfaces;
using Infrastructure.Repositories.Jira.Interfaces;
using Infrastructure.Repositories.Documents.Interfaces;
using Infrastructure.Services.Demo;
using Infrastructure.Repositories.Demo;
using Infrastructure.Repositories.Demo.Interfaces;
using Infrastructure.Observability;
using Infrastructure.Repositories.Risks;
using Infrastructure.Repositories.Risks.Interfaces;
using Infrastructure.Repositories.Organization.Interfaces;
using Infrastructure.Repositories.Sessions.Interfaces;
using System.Data;
using Application.Ports.Repositories;
using FluentMigrator.Runner;
using Infrastructure.Migrations;
using Infrastructure.Repositories.Identity;
using Infrastructure.Repositories.Organization;
using Infrastructure.Services.Identity;
using Infrastructure.Services.Risks;
using Infrastructure.Services.Jira;
using Infrastructure.Services.Documents;
using Infrastructure.Clients;
using Infrastructure.Repositories.RoleSystem;
using Infrastructure.Repositories.Sessions;
using Infrastructure.Repositories.Users;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Infrastructure;

/// <summary>
/// Регистрация зависимостей уровня Infrastructure
/// </summary>
public static class InfrastructureProviderBuilderExtensions
{
    /// <summary>
    /// Добавление зависимостей
    /// </summary>
    /// <param name="services">Контейнер зависимостей приложения</param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddMetrics();
        services.AddSingleton<OperationsMetrics>();
        services.AddScoped<ClientDatabaseContext>();
        services.AddScoped<IDbConnection>(provider =>
        {
            var connection = new NpgsqlConnection(provider.GetRequiredService<ClientDatabaseContext>().Client.ConnectionString);
            try
            {
                connection.Open();
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        });
        services.AddSingleton<IIdentityBootstrap, IdentityBootstrap>();
        services.AddSingleton<IBankingDemoSeeder, BankingDemoSeeder>();
        services.AddScoped<IBankingDemoRepository, BankingDemoRepository>();
        services.AddScoped<OrganizationRepository>();
        services.AddScoped<IOrganizationRepository>(provider => provider.GetRequiredService<OrganizationRepository>());
        services.AddScoped<IOrganizationIdentityRepository>(provider => provider.GetRequiredService<OrganizationRepository>());
        services.AddScoped<IIdentityAdministrationRepository, IdentityAdministrationRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<SessionRepository>();
        services.AddScoped<ISessionRepository>(provider => provider.GetRequiredService<SessionRepository>());
        services.AddScoped<ITransactionalSessionRepository>(provider => provider.GetRequiredService<SessionRepository>());
        services.AddSingleton<IJiraRestClient, JiraRestClient>();
        services.AddSingleton<IDocumentStorage, DocumentStorage>();
        services.AddScoped<RiskRepository>();
        services.AddScoped<RiskAssessmentRepository>();
        services.AddScoped<IRiskAssessmentRepository>(provider => provider.GetRequiredService<RiskAssessmentRepository>());
        services.AddScoped<RiskAssessmentStateRepository>();
        services.AddScoped<IRiskAssessmentStateRepository>(provider => provider.GetRequiredService<RiskAssessmentStateRepository>());
        services.AddScoped<RiskStateRepository>();
        services.AddScoped<IRiskStateRepository>(provider => provider.GetRequiredService<RiskStateRepository>());
        services.AddScoped<RiskReferenceRepository>();
        services.AddScoped<IRiskReferenceRepository>(provider => provider.GetRequiredService<RiskReferenceRepository>());
        services.AddScoped<BusinessHistoryRepository>();
        services.AddScoped<IBusinessHistoryRepository>(provider => provider.GetRequiredService<BusinessHistoryRepository>());
        services.AddScoped<TreatmentRepository>();
        services.AddScoped<ITreatmentRepository>(provider => provider.GetRequiredService<TreatmentRepository>());
        services.AddScoped<TreatmentStateRepository>();
        services.AddScoped<ITreatmentStateRepository>(provider => provider.GetRequiredService<TreatmentStateRepository>());
        services.AddScoped<TreatmentLifecycleRepository>();
        services.AddScoped<ITreatmentLifecycleRepository>(provider => provider.GetRequiredService<TreatmentLifecycleRepository>());
        services.AddScoped<JiraStateRepository>();
        services.AddScoped<IJiraStateRepository>(provider => provider.GetRequiredService<JiraStateRepository>());
        services.AddScoped<JiraRepository>();
        services.AddScoped<DocumentRepository>();
        services.AddScoped<ReviewRepository>();
        services.AddScoped<ReviewStateRepository>();
        services.AddScoped<IReviewStateRepository>(provider => provider.GetRequiredService<ReviewStateRepository>());
        services.AddScoped<IDocumentRepository>(provider => provider.GetRequiredService<DocumentRepository>());
        services.AddScoped<IDocumentWorkerRepository>(provider => provider.GetRequiredService<DocumentRepository>());
        services.AddHostedService<DocumentWorker>();
        services.AddScoped<IJiraRepository>(provider => provider.GetRequiredService<JiraRepository>());
        services.AddScoped<IJiraWorkerRepository>(provider => provider.GetRequiredService<JiraRepository>());
        services.AddHostedService<JiraWorker>();
        services.AddScoped<IRiskRepository>(provider => provider.GetRequiredService<RiskRepository>());
        services.AddScoped<IRiskOrganizationRepository>(provider => provider.GetRequiredService<RiskRepository>());
        services.AddScoped<IReviewRepository>(provider => provider.GetRequiredService<ReviewRepository>());
        services.AddScoped<ITransactionalReviewWorkerRepository>(provider => provider.GetRequiredService<ReviewRepository>());
        services.AddHostedService<BusinessHistoryWorker>();
        services.AddHostedService<RiskReviewWorker>();
        return services;
    }

    /// <summary>
    /// Регистрирует runner для одной базы в отдельном контейнере миграций
    /// </summary>
    /// <param name="services">Контейнер миграций клиентской базы</param>
    /// <param name="connectionString">Подключение владельца схемы</param>
    internal static IServiceCollection AddClientMigrations(this IServiceCollection services, string connectionString)
    {
        services.AddFluentMigratorCore()
            .ConfigureRunner(runner => runner.AddPostgres()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(Date_202611032300_AddIdentityTables).Assembly).For.Migrations())
            .AddLogging();
        
        return services;
    }
}
