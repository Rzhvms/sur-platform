using Infrastructure.Clients;
using Infrastructure.Repositories.Risks.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Documents;

/// <summary>Продолжает отчёты и импорт отдельно для каждой подготовленной базы</summary>
/// <param name="clients">Каталог клиентских баз</param>
/// <param name="initializer">Состояние подготовки схемы</param>
/// <param name="scopes">Изолированные клиентские контексты</param>
/// <param name="logger">Журнал фоновой службы</param>
internal sealed class DocumentWorker(ClientDatabaseCatalog clients, ClientDatabaseInitializer initializer,
    IServiceScopeFactory scopes, ILogger<DocumentWorker> logger) : BackgroundService
{
    /// <summary>Пауза между проходами по клиентской очереди</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(3);

    /// <summary>Сообщение без содержимого файла и сведений пользователя</summary>
    private const string RetryMessage = "Фоновая обработка документов {ClientHost}: {ErrorType}";

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var client in clients.Clients)
            {
                if (!initializer.IsPrepared(client))
                {
                    continue;
                }

                try
                {
                    using var scope = scopes.CreateScope();
                    scope.ServiceProvider.GetRequiredService<ClientDatabaseContext>().Select(client);
                    await scope.ServiceProvider.GetRequiredService<IDocumentWorkerRepository>().ProcessDocumentsAsync(stoppingToken);
                }
                catch (Exception error) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning(RetryMessage, client.Host, error.GetType().Name);
                }
            }
        }
    }
}
