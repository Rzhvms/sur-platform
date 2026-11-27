using Infrastructure.Services.Identity.Interfaces;
using Infrastructure.Constants;
using Infrastructure.Observability;
using System.ComponentModel.DataAnnotations;
using Application.Services.Authentication.Interfaces;
using Infrastructure.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Services.Identity;

/// <summary>
/// Выполняет настроенную выдачу первому администратору в scope выбранного клиента
/// </summary>
/// <param name="scopes">Фабрика изолированных клиентских scope</param>
/// <param name="configuration">Доверенная конфигурация развёртывания</param>
/// <param name="environment">Окружение, в котором разрешены тестовые реквизиты</param>
internal sealed class IdentityBootstrap(IServiceScopeFactory scopes, IConfiguration configuration, IHostEnvironment environment) : IIdentityBootstrap
{
    /// <summary>
    /// First admin email key
    /// </summary>
    private const string FirstAdminEmailKey = "FirstAdministrator:Email";

    /// <summary>
    /// First admin name key
    /// </summary>
    private const string FirstAdminNameKey = "FirstAdministrator:FullName";

    /// <summary>Пароль первого администратора для локального окружения</summary>
    private const string FirstAdminPasswordKey = "FirstAdministrator:DevelopmentPassword";

    /// <summary>Минимальная длина пароля для входа</summary>
    private const int MinPasswordLength = 8;

    /// <summary>Максимальная длина пароля для входа</summary>
    private const int MaxPasswordLength = 256;

    /// <summary>Причина отказа служебной выдачи при отключённой доставке</summary>
    private const string DevelopmentAccessRequired = "Настройте DevelopmentPassword первого администратора в окружении Development";

    /// <summary>
    /// Missing first admin
    /// </summary>
    private const string MissingFirstAdmin = "Настройте ФИО и адрес первого администратора клиента";

    /// <inheritdoc/>
    public async Task<string?> RunAsync(ClientDatabase client, bool reissue = false)
    {
        if (!reissue && configuration.GetValue(RecoverySettings.PauseOutbound, false))
        {
            return null;
        }

        var section = configuration.GetSection(ClientConfigurationKeys.Clients).GetChildren().Single(c =>
            string.Equals(c[ClientConfigurationKeys.Host], client.Host, StringComparison.OrdinalIgnoreCase));
        var email = section[FirstAdminEmailKey]?.Trim();
        var fullName = section[FirstAdminNameKey]?.Trim();
        if (!reissue && string.IsNullOrEmpty(email) && string.IsNullOrEmpty(fullName))
        {
            return null;
        }

        var password = environment.IsDevelopment() ? section[FirstAdminPasswordKey] : null;
        if (string.IsNullOrEmpty(password))
        {
            if (reissue)
            {
                throw new InvalidOperationException(DevelopmentAccessRequired);
            }

            return null;
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length is < MinPasswordLength or > MaxPasswordLength)
        {
            throw new InvalidOperationException(DevelopmentAccessRequired);
        }

        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || !new EmailAddressAttribute().IsValid(email) ||
            string.IsNullOrWhiteSpace(fullName) || fullName.Length > 250)
        {
            throw new InvalidOperationException(MissingFirstAdmin);
        }

        using var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<ClientDatabaseContext>().Select(client);
        return await scope.ServiceProvider.GetRequiredService<IIdentityAccessService>()
            .BootstrapAsync(email, fullName, password, reissue);
    }
}
