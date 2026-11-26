using Infrastructure.Repositories.Demo.Models;

namespace Infrastructure.Repositories.Demo.Interfaces;

/// <summary>Заполняет демоданные один раз в выбранной клиентской базе</summary>
internal interface IBankingDemoRepository
{
    /// <summary>Сохраняет весь связанный набор одной транзакцией без изменения прежних записей</summary>
    /// <param name="data">Проверенный набор из поставки приложения</param>
    /// <param name="password">Необязательный пароль новых демонстрационных учётных записей</param>
    /// <param name="cancellationToken">Остановка подготовки клиента</param>
    /// <returns>Признак первоначальной записи; false при уже установленном наборе</returns>
    Task<bool> SeedAsync(BankingDemoDataSet data, string? password, CancellationToken cancellationToken);
}
