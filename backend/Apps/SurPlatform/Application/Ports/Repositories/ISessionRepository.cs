using Domain.DalModels;
using Application.Models;

namespace Application.Ports.Repositories;

/// <summary>
/// Хранение сессий и истории refresh-токенов
/// </summary>
public interface ISessionRepository
{
    /// <summary>
    /// Создать сессию и ее первый refresh-токен атомарно
    /// </summary>
    /// <param name="session">Создаваемый сеанс</param>
    /// <param name="token">Первый refresh-токен</param>
    /// <param name="accessVersion">Версия доступа во время проверки пароля</param>
    /// <returns>Признак сохранения сессии</returns>
    Task<bool> CreateAsync(SessionDal session, RefreshTokenDal token, long accessVersion);

    /// <summary>
    /// Получение сессии
    /// </summary>
    Task<SessionDal?> GetAsync(Guid sessionId);

    /// <summary>
    /// Проверка принадлежности и действительность сессии активного пользователя
    /// </summary>
    /// <param name="userId">Пользователь, которому должна принадлежать сессия</param>
    /// <param name="sessionId">Проверяемая сессия</param>
    /// <param name="currentDateTime">Момент проверки в UTC</param>
    /// <param name="cancellationToken">Отмена запроса к базе</param>
    /// <returns>Признак действующей сессии</returns>
    /// <param name="accessVersion">Версия доступа из токена</param>
    /// <param name="rightCode">Право служебной операции или null</param>
    Task<bool> IsActiveAsync(Guid userId, Guid sessionId, DateTime currentDateTime, CancellationToken cancellationToken, long accessVersion, string? rightCode = null);

    /// <summary>
    /// Получить refresh-токен по хешу
    /// </summary>
    Task<RefreshTokenDal?> GetRefreshAsync(string hash);

    /// <summary>
    /// Получить данные refresh-токена, сессии и состояния пользователя одним запросом
    /// </summary>
    Task<RefreshSessionData?> GetRefreshSessionAsync(string tokenHash);

    /// <summary>
    /// Однократно заменяет действующий токен и обновляет время активности сессии
    /// </summary>
    /// <param name="tokenId">Токен для одноразовой замены</param>
    /// <param name="next">Следующий токен той же сессии</param>
    /// <param name="currentDateTime">Момент ротации в UTC</param>
    /// <param name="accessVersion">Версия состояния для нового токена</param>
    /// <returns>Признак успешной ротации</returns>
    Task<bool> RotateAsync(Guid tokenId, RefreshTokenDal next, DateTime currentDateTime, long accessVersion);

    /// <summary>
    /// Отозвать сессию и токены
    /// </summary>
    Task<bool> RevokeAsync(Guid sessionId, Guid userId, DateTime currentDateTime, string reason);

    /// <summary>
    /// Завершить все сессии пользователя
    /// </summary>
    Task RevokeUserSessionsAsync(Guid userId, DateTime currentDateTime, string reason);
}
