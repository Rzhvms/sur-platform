namespace Infrastructure.Clients;

/// <summary>
/// Привязка одного DI scope к клиентской базе до разрешения репозиториев
/// </summary>
public sealed class ClientDatabaseContext
{
    /// <summary>
    /// Unselected database
    /// </summary>
    private const string UnselectedDatabase = "Клиентская база не выбрана";

    /// <summary>
    /// Already selected database
    /// </summary>
    private const string AlreadySelectedDatabase = "Клиентская база уже выбрана";

    /// <summary>
    /// Выбранная для scope база
    /// </summary>
    private ClientDatabase? _client;

    /// <summary>
    /// Клиент, назначенный входной точкой или обработчиком
    /// </summary>
    public ClientDatabase Client
    {
        get { return _client ?? throw new InvalidOperationException(UnselectedDatabase); }
    }

    /// <summary>
    /// Фиксирует базу на всё время жизни scope
    /// </summary>
    /// <param name="client">Клиент из реестра конфигурации</param>
    /// <exception cref="InvalidOperationException">В scope уже выбрана база</exception>
    public void Select(ClientDatabase client)
    {
        if (_client is not null)
        {
            throw new InvalidOperationException(AlreadySelectedDatabase);
        }

        _client = client;
    }
}
