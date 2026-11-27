namespace Application.UseCases.System.Interfaces;

/// <summary>
/// Менеджет получения технических метаданных
/// </summary>
public interface ISystemUseCaseManager
{
    /// <summary>
    /// Проверяет, что рабочее подключение видит все требуемые миграции
    /// </summary>
    Task<bool> IsDatabaseReadyAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Получить issuer для конфигурации текущего хоста
    /// </summary>
    string GetIssuer();

    /// <summary>
    /// Получить модуль и экспоненту публичного ключа для JWKS
    /// </summary>
    (string Module, string Exponent) GetSigningKey();
}