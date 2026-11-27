using Infrastructure.Clients;

namespace Infrastructure.Services.Demo.Interfaces;

/// <summary>
/// Первоначальное заполнение демонстрационных данных выбранного клиента
/// </summary>
public interface IBankingDemoSeeder
{
    /// <summary>Заполняет одну подготовленную клиентскую базу, если оператор включил настройку</summary>
    /// <param name="client">Запись доверенного каталога клиентов</param>
    /// <param name="cancellationToken">Остановка приложения</param>
    /// <exception cref="InvalidOperationException">Набор или настройки недопустимы</exception>
    Task RunAsync(ClientDatabase client, CancellationToken cancellationToken);
}
