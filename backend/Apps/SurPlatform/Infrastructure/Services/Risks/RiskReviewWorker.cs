using Infrastructure.Repositories.Risks.Interfaces;
using Infrastructure.Clients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Risks;

/// <summary>Регистрирует календарные даты и возобновляет сохранённые запуски пересмотра</summary>
/// <param name="clients">Каталог клиентских баз</param>
/// <param name="initializer">Состояние подготовки схемы</param>
/// <param name="scopes">Отдельные клиентские контексты</param>
/// <param name="logger">Технический журнал фоновых запусков</param>
public sealed class RiskReviewWorker(ClientDatabaseCatalog clients, ClientDatabaseInitializer initializer,
    IServiceScopeFactory scopes, ILogger<RiskReviewWorker> logger) : BackgroundService
{
    /// <summary>Пауза между попытками переноса</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    /// <summary>Сообщение о повторной попытке без данных внешней системы</summary>
    private const string RetryMessage = "Обработка пересмотров клиента {ClientHost} будет повторён: {ErrorType}";

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
                    await scope.ServiceProvider.GetRequiredService<ITransactionalReviewWorkerRepository>().ProcessReviewsAsync(stoppingToken);
                }
                catch (Exception error) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning(RetryMessage, client.Host, error.GetType().Name);
                }
            }
        }
    }
}
