using Infrastructure.Clients;

namespace Infrastructure.Services.Identity.Interfaces;

/// <summary>
/// Подготовка первоначального административного доступа выбранного клиента
/// </summary>
public interface IIdentityBootstrap
{
    /// <summary>
    /// Ищет параметры первого администратора по каноническому хосту и применяет команду
    /// </summary>
    /// <param name="client">Клиент, база которого подготовлена</param>
    /// <param name="reissue">Явная повторная выдача от оператора развёртывания</param>
    /// <returns>Состояние локальной выдачи либо null, если она пропущена</returns>
    /// <exception cref="InvalidOperationException">Некорректные параметры первого администратора</exception>
    Task<string?> RunAsync(ClientDatabase client, bool reissue = false);
}
