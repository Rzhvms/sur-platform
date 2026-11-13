using Application.Constants;
using Infrastructure.Clients;
using Infrastructure.Services.Identity;

namespace Api;

/// <summary>
/// Точка входа приложения и служебной выдачи доступа
/// </summary>
internal static class Program
{
    /// <summary>
    /// Reissue first admin option
    /// </summary>
    private const string ReissueFirstAdminOption = "--reissue-first-admin";

    /// <summary>
    /// Missing host
    /// </summary>
    private const string MissingHost = "Укажите хост клиента для повторной выдачи";

    /// <summary>
    /// Unknown client
    /// </summary>
    private const string UnknownClient = "Клиент не зарегистрирован";

    /// <summary>
    /// Reissue failure format
    /// </summary>
    private const string ReissueFailureFormat = "Повторная выдача завершилась ошибкой: {0}";

    /// <summary>
    /// Запускает HTTP-хост или повторно выдаёт доступ первому администратору клиента
    /// </summary>
    /// <param name="args">Параметры запуска или команда повторной выдачи с хостом клиента</param>
    /// <returns>Код завершения процесса</returns>
    public static async Task<int> Main(string[] args)
    {
        var reissue = args.Length > 0 && args[0] == ReissueFirstAdminOption;
        if (reissue && args.Length != 2)
        {
            Console.Error.WriteLine(MissingHost);
            return 1;
        }

        using var host = Host.CreateDefaultBuilder(reissue ? [] : args)
            .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>())
            .Build();
        if (!reissue)
        {
            await host.RunAsync();
            return 0;
        }

        var client = host.Services.GetRequiredService<ClientDatabaseCatalog>().Find(args[1]);
        if (client is null)
        {
            Console.Error.WriteLine(UnknownClient);
            return 1;
        }

        try
        {
            await ClientDatabaseInitializer.PrepareAsync(client, CancellationToken.None);
            var status = await host.Services.GetRequiredService<IdentityBootstrap>().RunAsync(client, true);
            Console.WriteLine(status);
            return status == IdentityAccounts.InvitationSent ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, ReissueFailureFormat, ex.GetType().Name));
            return 1;
        }
    }
}
