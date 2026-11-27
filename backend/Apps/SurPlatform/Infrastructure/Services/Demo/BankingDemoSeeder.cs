using Infrastructure.Services.Demo.Interfaces;
using System.Text.Json;
using Application.Constants;
using Infrastructure.Clients;
using Infrastructure.Observability;
using Infrastructure.Repositories.Demo.Interfaces;
using Infrastructure.Repositories.Demo.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Services.Demo;

/// <summary>Выбирает доверенный клиентский контекст для явного заполнения демоданных</summary>
/// <param name="scopes">Фабрика клиентских областей зависимостей</param>
/// <param name="configuration">Настройки запуска приложения</param>
internal sealed class BankingDemoSeeder(IServiceScopeFactory scopes, IConfiguration configuration) : IBankingDemoSeeder
{
    /// <summary>Выключенный по умолчанию переключатель первоначального заполнения</summary>
    private const string ActivationKey = "ActivateDemoDataSeed";

    /// <summary>Необязательный секрет только для новых демонстрационных учётных записей</summary>
    private const string PasswordKey = "DemoDataSeedPassword";

    /// <summary>Неизменяемый ресурс поставки без реальных реквизитов и секретов</summary>
    private const string ResourceName = "Infrastructure.SeedData.BankingDemo.json";

    /// <summary>Ошибка целостности поставки</summary>
    private const string MissingData = "Отсутствует банковский демонстрационный набор";

    /// <summary>Некорректная конфигурация демонстрационного пароля</summary>
    private const string InvalidPassword = "DemoDataSeedPassword должен содержать от 8 до 256 символов и не должен состоять только из пробелов";

    /// <inheritdoc/>
    public async Task RunAsync(ClientDatabase client, CancellationToken cancellationToken)
    {
        if (!configuration.GetValue(ActivationKey, false) || configuration.GetValue(RecoverySettings.PauseOutbound, false))
        {
            return;
        }
        var password = configuration[PasswordKey];
        if (!string.IsNullOrEmpty(password) && (string.IsNullOrWhiteSpace(password) ||
            password.Length < IdentityAccounts.MinPasswordLength || password.Length > IdentityAccounts.MaxPasswordLength))
        {
            throw new InvalidOperationException(InvalidPassword);
        }
        // Ресурс берётся из сборки, путь/SQL не могут быть подменены внешним конфигом.
        await using var stream = typeof(BankingDemoSeeder).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(MissingData);
        var data = await JsonSerializer.DeserializeAsync<BankingDemoDataSet>(stream, JsonSerializerOptions.Web, cancellationToken)
            ?? throw new InvalidOperationException(MissingData);
        using var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<ClientDatabaseContext>().Select(client);
        await scope.ServiceProvider.GetRequiredService<IBankingDemoRepository>().SeedAsync(data, password, cancellationToken);
    }
}
