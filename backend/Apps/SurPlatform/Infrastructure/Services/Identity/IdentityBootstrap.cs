using Infrastructure.Constants;
using System.ComponentModel.DataAnnotations;
using Application.Services.Authentication;
using Infrastructure.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Services.Identity;

/// <summary>
/// Выполняет настроенную выдачу первому администратору в scope выбранного клиента
/// </summary>
/// <param name="scopes">Фабрика изолированных клиентских scope</param>
/// <param name="configuration">Доверенная конфигурация развёртывания</param>
public sealed class IdentityBootstrap(IServiceScopeFactory scopes, IConfiguration configuration)
{
    /// <summary>
    /// First admin email key
    /// </summary>
    private const string FirstAdminEmailKey = "FirstAdministrator:Email";

    /// <summary>
    /// First admin name key
    /// </summary>
    private const string FirstAdminNameKey = "FirstAdministrator:FullName";

    /// <summary>
    /// Missing first admin
    /// </summary>
    private const string MissingFirstAdmin = "Настройте ФИО и адрес первого администратора клиента";

    /// <summary>
    /// Ищет параметры первого администратора по каноническому хосту и применяет команду
    /// </summary>
    /// <param name="client">Клиент, база которого подготовлена</param>
    /// <param name="reissue">Явная повторная выдача от оператора развёртывания</param>
    /// <returns>Состояние отправки либо null для уже созданной записи</returns>
    /// <exception cref="InvalidOperationException">Некорректные параметры первого администратора</exception>
    public async Task<string?> RunAsync(ClientDatabase client, bool reissue = false)
    {
        var section = configuration.GetSection(ClientConfigurationKeys.Clients).GetChildren().Single(c =>
            string.Equals(c[ClientConfigurationKeys.Host], client.Host, StringComparison.OrdinalIgnoreCase));
        var email = section[FirstAdminEmailKey]?.Trim();
        var fullName = section[FirstAdminNameKey]?.Trim();
        if (!reissue && string.IsNullOrEmpty(email) && string.IsNullOrEmpty(fullName))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || !new EmailAddressAttribute().IsValid(email) ||
            string.IsNullOrWhiteSpace(fullName) || fullName.Length > 250)
        {
            throw new InvalidOperationException(MissingFirstAdmin);
        }

        using var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<ClientDatabaseContext>().Select(client);
        return await scope.ServiceProvider.GetRequiredService<IdentityInvitationService>()
            .BootstrapAsync(email, fullName, reissue);
    }
}
