namespace Infrastructure.Clients;

/// <summary>
/// Подключения одной клиентской базы и её канонический HTTPS-адрес
/// </summary>
/// <param name="Host">Зарегистрированное имя хоста</param>
/// <param name="ConnectionString">Рабочее подключение приложения</param>
/// <param name="MigrationConnectionString">Подключение владельца схемы</param>
/// <param name="BootstrapConnectionString">Подключение к служебной базе с правом создания БД</param>
public sealed class ClientDatabase(
    string Host,
    string ConnectionString,
    string MigrationConnectionString,
    string? BootstrapConnectionString)
{
    /// <summary>
    /// Разделитель схемы и хоста в адресе issuer
    /// </summary>
    private const string SchemeDelimiter = "://";

    /// <summary>
    /// Имя хоста из реестра клиентов
    /// </summary>
    public string Host { get; } = Host;

    /// <summary>
    /// Издатель токенов для клиентского хоста
    /// </summary>
    public string Issuer { get; } = string.Concat(Uri.UriSchemeHttps, SchemeDelimiter, Host);

    /// <summary>
    /// Подключение прикладных репозиториев
    /// </summary>
    internal string ConnectionString { get; } = ConnectionString;

    /// <summary>
    /// Подключение runner миграций
    /// </summary>
    internal string MigrationConnectionString { get; } = MigrationConnectionString;

    /// <summary>
    /// Подключение для первоначального создания БД
    /// </summary>
    internal string? BootstrapConnectionString { get; } = BootstrapConnectionString;
}
