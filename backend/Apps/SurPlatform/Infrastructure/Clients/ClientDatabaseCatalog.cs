using Infrastructure.Constants;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Infrastructure.Clients;

/// <summary>
/// Загружает список клиентских баз из доверенной конфигурации запуска
/// </summary>
public sealed class ClientDatabaseCatalog
{
    /// <summary>
    /// Invalid dns name
    /// </summary>
    private const string InvalidDnsName = "В Clients требуется каноническое DNS-имя без порта";

    /// <summary>
    /// Connection string key
    /// </summary>
    private const string ConnectionStringKey = "ConnectionString";

    /// <summary>
    /// Migration connection string key
    /// </summary>
    private const string MigrationConnectionStringKey = "MigrationConnectionString";

    /// <summary>
    /// Inconsistent database format
    /// </summary>
    private const string InconsistentDatabaseFormat = "Для клиента {0} требуется отдельная база и согласованное подключение миграций";

    /// <summary>
    /// Bootstrap connection string key
    /// </summary>
    private const string BootstrapConnectionStringKey = "BootstrapConnectionString";

    /// <summary>
    /// Invalid maintenance database format
    /// </summary>
    private const string InvalidMaintenanceDatabaseFormat = "Для клиента {0} требуется отдельная служебная база на том же сервере";

    /// <summary>
    /// Duplicate host format
    /// </summary>
    private const string DuplicateHostFormat = "Хост {0} зарегистрирован более одного раза";

    /// <summary>
    /// Missing clients
    /// </summary>
    private const string MissingClients = "Настройте хотя бы одну клиентскую базу в Clients";

    /// <summary>
    /// Invalid connection string format
    /// </summary>
    private const string InvalidConnectionStringFormat = "Неверная строка подключения клиента {0}";

    /// <summary>
    /// Missing connection fields format
    /// </summary>
    private const string MissingConnectionFieldsFormat = "Для клиента {0} явно задайте один сервер, базу и роль PostgreSQL";

    /// <summary>
    /// Utc zone
    /// </summary>
    private const string UtcZone = "UTC";

    /// <summary>
    /// Клиенты по каноническому имени хоста
    /// </summary>
    private readonly Dictionary<string, ClientDatabase> _clients = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Клиенты для подготовки баз и явного выбора в фоновых scope
    /// </summary>
    public IReadOnlyCollection<ClientDatabase> Clients => _clients.Values;

    /// <summary>
    /// Проверяет уникальность хостов и настроенных назначений баз
    /// </summary>
    /// <param name="configuration">Конфигурация запуска приложения</param>
    /// <exception cref="InvalidOperationException">Не задан клиент или его настройки противоречивы</exception>
    public ClientDatabaseCatalog(IConfiguration configuration)
    {
        var databaseNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var section in configuration.GetSection(ClientConfigurationKeys.Clients).GetChildren())
        {
            var host = section[ClientConfigurationKeys.Host]?.ToLowerInvariant();
            if (host is null || Uri.CheckHostName(host) != UriHostNameType.Dns ||
                host.EndsWith('.') || host.Any(c => c > 127))
            {
                throw new InvalidOperationException(InvalidDnsName);
            }

            var working = ParseConnection(section[ConnectionStringKey], host);
            var migration = ParseConnection(section[MigrationConnectionStringKey] ?? working.ConnectionString, host);
            if (Target(working) != Target(migration) || !databaseNames.Add(working.Database ?? string.Empty))
            {
                throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.InvariantCulture, InconsistentDatabaseFormat, host));
            }

            var bootstrap = section[BootstrapConnectionStringKey];
            if (bootstrap is not null)
            {
                var maintenance = ParseConnection(bootstrap, host);
                if (maintenance.Database == working.Database ||
                    !string.Equals(maintenance.Host, working.Host, StringComparison.OrdinalIgnoreCase) ||
                    maintenance.Port != working.Port)
                {
                    throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.InvariantCulture, InvalidMaintenanceDatabaseFormat, host));
                }

                bootstrap = maintenance.ConnectionString;
            }

            if (!_clients.TryAdd(host, new ClientDatabase(
                host, working.ConnectionString, migration.ConnectionString, bootstrap)))
            {
                throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.InvariantCulture, DuplicateHostFormat, host));
            }
        }

        if (_clients.Count == 0)
        {
            throw new InvalidOperationException(MissingClients);
        }
    }

    /// <summary>
    /// Ищет клиентскую базу по разрешённому хосту
    /// </summary>
    /// <param name="host">Проверяемое имя без порта</param>
    /// <returns>Клиент либо null для неизвестного имени</returns>
    public ClientDatabase? Find(string host)
    {
        return _clients.GetValueOrDefault(host);
    }

    /// <summary>
    /// Разбирает подключение и проверяет явный адрес базы
    /// </summary>
    /// <param name="value">Строка подключения из конфигурации</param>
    /// <param name="host">Хост клиента для сообщения об ошибке</param>
    /// <returns>Проверенные параметры подключения</returns>
    /// <exception cref="InvalidOperationException">Подключение неполное или содержит несколько серверов</exception>
    private static NpgsqlConnectionStringBuilder ParseConnection(string? value, string host)
    {
        NpgsqlConnectionStringBuilder connection;
        try
        {
            connection = new NpgsqlConnectionStringBuilder(value);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.InvariantCulture, InvalidConnectionStringFormat, host));
        }

        if (string.IsNullOrWhiteSpace(connection.Host) || connection.Host.Contains(',') ||
            string.IsNullOrWhiteSpace(connection.Database) ||
            System.Text.Encoding.UTF8.GetByteCount(connection.Database) > 63 ||
            string.IsNullOrWhiteSpace(connection.Username))
        {
            throw new InvalidOperationException(string.Format(System.Globalization.CultureInfo.InvariantCulture, MissingConnectionFieldsFormat, host));
        }

        // Колонки Identity timestamp без часового пояса хранят UTC
        connection.Timezone = UtcZone;
        return connection;
    }

    /// <summary>
    /// Возвращает адрес базы без учётных данных
    /// </summary>
    /// <param name="connection">Проверенное подключение</param>
    /// <returns>Сервер, порт и имя базы</returns>
    private static (string Server, int Port, string Database) Target(NpgsqlConnectionStringBuilder connection)
    {
        return (connection.Host?.ToLowerInvariant() ?? string.Empty, connection.Port, connection.Database ?? string.Empty);
    }
}
