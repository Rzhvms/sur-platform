using System.Data;
using Infrastructure.Observability;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Clients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Identity;

/// <summary>Переносит клиентские поручения в неизменяемые события без повторных записей</summary>
/// <param name="clients">Каталог клиентских баз</param>
/// <param name="initializer">Состояние подготовки схемы</param>
/// <param name="scopes">Отдельные клиентские контексты</param>
/// <param name="logger">Технический журнал фонового переноса</param>
public sealed class BusinessHistoryWorker(ClientDatabaseCatalog clients, ClientDatabaseInitializer initializer,
    IServiceScopeFactory scopes, ILogger<BusinessHistoryWorker> logger) : BackgroundService
{
    /// <summary>Измеряемая очередь переноса истории</summary>
    private const string HistoryOperation = "history";

    /// <summary>Пауза между попытками переноса</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    /// <summary>Максимум поручений за проход для одного клиента</summary>
    private const int BatchSize = 100;

    /// <summary>Сообщение о повторной попытке без данных бизнес-события</summary>
    private const string RetryMessage = "Перенос истории клиента {ClientHost} будет повторён: {ErrorType}";

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
                    await TransferAsync(scope.ServiceProvider.GetRequiredService<IDbConnection>(),
                        scope.ServiceProvider.GetRequiredService<OperationsMetrics>(), stoppingToken);
                }
                catch (Exception error) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning(RetryMessage, client.Host, error.GetType().Name);
                }
            }
        }
    }

    /// <summary>Захватывает порцию поручений, переносит и удаляет их одной транзакцией</summary>
    /// <param name="connection">Подключение одного клиента</param>
    /// <param name="operations">Метрики возраста поручений</param>
    /// <param name="cancellationToken">Отмена при остановке</param>
    private static async Task TransferAsync(IDbConnection connection, OperationsMetrics operations, CancellationToken cancellationToken)
    {
        using var transaction = connection.BeginTransaction();
        var limit = BatchSize;
        var selectQuery = $"""
            SELECT {EntityMapper.ColName<BusinessChangeJobDal>(x => x.EventId)}, {EntityMapper.ColName<BusinessChangeJobDal>(x => x.OccurredAt)} FROM {EntityMapper.TbName<BusinessChangeJobDal>()}
            ORDER BY {EntityMapper.ColName<BusinessChangeJobDal>(x => x.OccurredAt)}, {EntityMapper.ColName<BusinessChangeJobDal>(x => x.EventId)} LIMIT {EntityMapper.ParameterName(nameof(limit))} FOR UPDATE SKIP LOCKED
            """;
        var jobs = (await connection.QueryAsync<BusinessChangeJobDal>(new CommandDefinition(selectQuery, new { limit },
            transaction, cancellationToken: cancellationToken))).ToArray();
        if (jobs.Length > 0)
        {
            var ids = jobs.Select(job => job.EventId).ToArray();
            var transferQuery = $"""
                INSERT INTO {EntityMapper.TbName<BusinessChangeDal>()} ({EntityMapper.ColName<BusinessChangeDal>(x => x.EventId)}, {EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)}, {EntityMapper.ColName<BusinessChangeDal>(x => x.Payload)})
                SELECT {EntityMapper.ColName<BusinessChangeJobDal>(x => x.EventId)}, {EntityMapper.ColName<BusinessChangeJobDal>(x => x.OccurredAt)}, {EntityMapper.ColName<BusinessChangeJobDal>(x => x.Payload)}
                FROM {EntityMapper.TbName<BusinessChangeJobDal>()} WHERE {EntityMapper.ColName<BusinessChangeJobDal>(x => x.EventId)} = ANY({EntityMapper.ParameterName(nameof(ids))})
                ON CONFLICT ({EntityMapper.ColName<BusinessChangeDal>(x => x.EventId)}) DO NOTHING
                """;
            var cleanupQuery = $"""
                DELETE FROM {EntityMapper.TbName<BusinessChangeJobDal>()} j WHERE j.{EntityMapper.ColName<BusinessChangeJobDal>(x => x.EventId)} = ANY({EntityMapper.ParameterName(nameof(ids))})
                AND EXISTS (SELECT FROM {EntityMapper.TbName<BusinessChangeDal>()} e WHERE e.{EntityMapper.ColName<BusinessChangeDal>(x => x.EventId)} = j.{EntityMapper.ColName<BusinessChangeJobDal>(x => x.EventId)})
                """;
            await connection.ExecuteAsync(new CommandDefinition(transferQuery, new { ids }, transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(cleanupQuery, new { ids }, transaction, cancellationToken: cancellationToken));
        }

        transaction.Commit();
        if (jobs.Length > 0)
        {
            operations.Age(HistoryOperation, jobs[0].OccurredAt);
        }
    }
}
