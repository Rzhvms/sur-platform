using System.Data;
using Application.Ports.Repositories;
using FluentMigrator.Runner;
using Infrastructure.Migrations;
using Infrastructure.Repositories.RoleSystem;
using Infrastructure.Repositories.Sessions;
using Infrastructure.Repositories.Users;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Infrastructure;

/// <summary>
/// Регистрация зависимостей уровня Infrastructure
/// </summary>
public static class InfrastructureStartUp
{
    /// <summary>
    /// Добавление зависимостей
    /// </summary>
    public static void AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddScoped<IDbConnection>(_ =>
        {
            var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            return connection;
        });
        services.AddFluentMigratorCore()
            .ConfigureRunner(runner => runner.AddPostgres()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(Date_202611032300_AddIdentityTables).Assembly).For.Migrations())
            .AddLogging(logging => logging.AddFluentMigratorConsole());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
    }
}
