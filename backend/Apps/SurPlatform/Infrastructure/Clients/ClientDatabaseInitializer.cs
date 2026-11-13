using Core.Database.DapperExtensions.EntityMapper;
using FluentMigrator.Runner;
using Infrastructure.Services.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure.Clients;

/// <summary>
/// Подготавливает базы по очереди и хранит результат начальной проверки схемы
/// </summary>
/// <param name="catalog">Реестр настроенных клиентов</param>
/// <param name="bootstrap">Первоначальная выдача доступа</param>
/// <param name="logger">Журнал состояния баз без реквизитов</param>
public sealed class ClientDatabaseInitializer(ClientDatabaseCatalog catalog, IdentityBootstrap bootstrap, ILogger<ClientDatabaseInitializer> logger)
    : BackgroundService
{
    /// <summary>
    /// Preparation error
    /// </summary>
    private const string PreparationError = "База клиента {ClientHost} не готова, ошибка {ErrorType}, SQLSTATE {SqlState}";

    /// <summary>
    /// Missing migration
    /// </summary>
    private const string MissingMigration = "Клиентская база не содержит требуемую миграцию";

    /// <summary>
    /// Параметр блокировки миграций
    /// </summary>
    private const string LockParameter = "key";

    /// <summary>
    /// Параметр имени клиентской базы
    /// </summary>
    private const string DatabaseParameter = "database";

    /// <summary>
    /// Системный каталог PostgreSQL
    /// </summary>
    private const string DatabaseCatalogTable = "pg_database";

    /// <summary>
    /// Имя базы в каталоге PostgreSQL
    /// </summary>
    private const string DatabaseNameColumn = "datname";

    /// <summary>
    /// Таблица версий FluentMigrator по умолчанию
    /// </summary>
    private const string MigrationVersionTable = "VersionInfo";

    /// <summary>
    /// Столбец номера миграции FluentMigrator
    /// </summary>
    private const string MigrationVersionColumn = "Version";

    /// <summary>
    /// Предельное число попыток при временной недоступности PostgreSQL
    /// </summary>
    private const int MaxAttempts = 5;

    /// <summary>
    /// Ключ блокировки миграций в пределах одной базы PostgreSQL
    /// </summary>
    private const long MigrationLockKey = 202611140000;

    /// <summary>
    /// Хосты, для которых прошла первоначальная подготовка
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, long[]> _ready = new();

    /// <summary>
    /// Проверяет завершение начальной подготовки указанной базы
    /// </summary>
    /// <param name="client">Клиент из реестра</param>
    /// <returns>Признак успешно подготовленной базы</returns>
    public bool IsPrepared(ClientDatabase client)
    {
        return _ready.ContainsKey(client.Host);
    }

    /// <summary>
    /// Проверяет доступность рабочей роли и наличие всех требуемых миграций
    /// </summary>
    /// <param name="client">Проверяемая клиентская база</param>
    /// <param name="cancellationToken">Отмена проверки</param>
    /// <returns>Признак готовности рабочей схемы</returns>
    public async Task<bool> IsReadyAsync(ClientDatabase client, CancellationToken cancellationToken)
    {
        if (!_ready.TryGetValue(client.Host, out var requiredVersions))
        {
            return false;
        }

        try
        {
            await CheckSchemaAsync(client, requiredVersions, cancellationToken);
            return true;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            _ready.TryRemove(client.Host, out _);
            return false;
        }
        catch (NpgsqlException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            _ready.TryRemove(client.Host, out _);
            return false;
        }
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var client in catalog.Clients)
        {
            stoppingToken.ThrowIfCancellationRequested();
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    var versions = await PrepareAsync(client, stoppingToken);
                    await bootstrap.RunAsync(client);
                    _ready.TryAdd(client.Host, versions);
                    break;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (NpgsqlException ex) when (ex.IsTransient && attempt < MaxAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(PreparationError,
                        client.Host, ex.GetType().Name, (ex as PostgresException)?.SqlState);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Создаёт отсутствующую базу и применяет миграции под выделенным подключением
    /// </summary>
    /// <param name="client">Клиент, для которого выполняется подготовка</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    /// <returns>Версии миграций, обязательные для работы этого хоста</returns>
    public static async Task<long[]> PrepareAsync(ClientDatabase client, CancellationToken cancellationToken)
    {
        if (client.BootstrapConnectionString is not null)
        {
            await CreateDatabaseAsync(client, cancellationToken);
        }

        var services = new ServiceCollection();
        services.AddClientMigrations(client.MigrationConnectionString);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Блокировка держится на отдельном открытом подключении, пока работает runner
        var lockConnectionString = new NpgsqlConnectionStringBuilder(client.MigrationConnectionString)
        {
            Pooling = false
        };
        await using var migrationLock = new NpgsqlConnection(lockConnectionString.ConnectionString);
        await migrationLock.OpenAsync(cancellationToken);
        await using var lockCommand = migrationLock.CreateCommand();
        var lockQuery = $"SELECT pg_advisory_lock({EntityMapper.ParameterName(LockParameter)})";
        lockCommand.CommandText = lockQuery;
        lockCommand.Parameters.AddWithValue(LockParameter, MigrationLockKey);
        await lockCommand.ExecuteNonQueryAsync(cancellationToken);

        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();
        var requiredVersions = runner.MigrationLoader.LoadMigrations().Keys.ToArray();
        await CheckSchemaAsync(client, requiredVersions, cancellationToken);
        return requiredVersions;
    }

    /// <summary>
    /// Создаёт базу при отсутствии, сохраняя существующие базы и записи
    /// </summary>
    /// <param name="client">Клиент с настроенным служебным подключением</param>
    /// <param name="cancellationToken">Отмена SQL-команд</param>
    private static async Task CreateDatabaseAsync(ClientDatabase client, CancellationToken cancellationToken)
    {
        var target = new NpgsqlConnectionStringBuilder(client.MigrationConnectionString);
        await using var connection = new NpgsqlConnection(client.BootstrapConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var check = connection.CreateCommand();
        var databaseExistsQuery = $"""
            SELECT EXISTS (SELECT FROM {EntityMapper.Identifier(DatabaseCatalogTable)}
                WHERE {EntityMapper.Identifier(DatabaseNameColumn)} = {EntityMapper.ParameterName(DatabaseParameter)})
            """;
        check.CommandText = databaseExistsQuery;
        check.Parameters.AddWithValue(DatabaseParameter, target.Database ?? string.Empty);
        if (await check.ExecuteScalarAsync(cancellationToken) is true)
        {
            return;
        }

        await using var create = connection.CreateCommand();
        var createDatabaseQuery = $"CREATE DATABASE {EntityMapper.Identifier(target.Database ?? string.Empty)} " +
            $"OWNER {EntityMapper.Identifier(target.Username ?? string.Empty)}";
        create.CommandText = createDatabaseQuery;
        try
        {
            await create.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.DuplicateDatabase or PostgresErrorCodes.UniqueViolation)
        {
            if (await check.ExecuteScalarAsync(cancellationToken) is not true)
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Читает версию схемы теми же реквизитами, что используют репозитории
    /// </summary>
    /// <param name="client">Клиентская база</param>
    /// <param name="requiredVersions">Миграции, известные текущему приложению</param>
    /// <param name="cancellationToken">Отмена SQL-запроса</param>
    /// <exception cref="InvalidOperationException">Требуемая миграция отсутствует</exception>
    private static async Task CheckSchemaAsync(ClientDatabase client, long[] requiredVersions, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(client.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var check = connection.CreateCommand();
        var versionColumn = EntityMapper.Identifier(MigrationVersionColumn);
        var checkVersionsQuery = $"""
            SELECT count(DISTINCT {versionColumn}) FROM {EntityMapper.Identifier(MigrationVersionTable)}
            WHERE {versionColumn} = ANY({EntityMapper.ParameterName(nameof(requiredVersions))})
            """;
        check.CommandText = checkVersionsQuery;
        check.Parameters.AddWithValue(nameof(requiredVersions), requiredVersions);
        if (await check.ExecuteScalarAsync(cancellationToken) is not long count || count != requiredVersions.Length)
        {
            throw new InvalidOperationException(MissingMigration);
        }
    }
}
