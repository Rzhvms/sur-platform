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
    Task CreateAsync(SessionDal session, RefreshTokenDal token);

    /// <summary>
    /// Получение сессии
    /// </summary>
    Task<SessionDal?> GetAsync(Guid sessionId);

    /// <summary>
    /// Проверка принадлежности и действительность сессии активного пользователя
    /// </summary>
    Task<bool> IsActiveAsync(Guid userId, Guid sessionId, DateTime currentDateTime);

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
    Task<bool> RotateAsync(Guid tokenId, RefreshTokenDal next, DateTime currentDateTime);

    /// <summary>
    /// Отозвать сессию и токены
    /// </summary>
    Task<bool> RevokeAsync(Guid sessionId, Guid userId, DateTime currentDateTime, string reason);

    /// <summary>
    /// Завершить все сессии пользователя
    /// </summary>
    Task RevokeUserSessionsAsync(Guid userId, DateTime currentDateTime, string reason);
}