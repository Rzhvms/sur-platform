using System.Data;
using Application.Ports.Repositories;
using FluentMigrator.Runner;
using Infrastructure.Migrations;
using Application.Ports.Notifications;
using Infrastructure.Repositories.Identity;
using Infrastructure.Repositories.Organization;
using Infrastructure.Services.Identity;
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
        services.AddSingleton<IdentityBootstrap>();
        services.AddScoped<OrganizationRepository>();
        services.AddScoped<IOrganizationRepository>(provider => provider.GetRequiredService<OrganizationRepository>());
        services.AddScoped<IIdentityAdministrationRepository, IdentityAdministrationRepository>();
        services.AddScoped<IIdentityInvitationSender, SmtpIdentityInvitationSender>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
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
