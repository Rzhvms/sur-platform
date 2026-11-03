using Domain.DalModels;
using Application.Models;

namespace Application.Ports.Repositories;

/// <summary>
/// Хранение сессий и истории refresh-токенов
/// </summary>
public interface ISessionRepository
{
    /// <summary>
    /// Создаёт сессию и её первый refresh-токен атомарно
    /// </summary>
    Task CreateAsync(SessionDal session, RefreshTokenDal token);

    /// <summary>
    /// Находит сессию
    /// </summary>
    Task<SessionDal?> GetAsync(Guid sessionId);

    /// <summary>
    /// Проверяет принадлежность и действительность сессии активного пользователя
    /// </summary>
    Task<bool> IsActiveAsync(Guid userId, Guid sessionId, DateTime now, CancellationToken cancellationToken);

    /// <summary>
    /// Находит refresh-токен по хешу
    /// </summary>
    Task<RefreshTokenDal?> GetRefreshAsync(string hash);

    /// <summary>
    /// Получает данные refresh-токена, сессии и состояния пользователя одним запросом
    /// </summary>
    Task<RefreshSessionData?> GetRefreshSessionAsync(string tokenHash);

    /// <summary>
    /// Однократно заменяет действующий токен и обновляет время активности сессии
    /// </summary>
    Task<bool> RotateAsync(Guid tokenId, RefreshTokenDal next, DateTime now);

    /// <summary>
    /// Завершает сессию и отзывает её токены
    /// </summary>
    Task<bool> RevokeAsync(Guid sessionId, Guid userId, DateTime now, string reason);

    /// <summary>
    /// Завершает все сессии пользователя
    /// </summary>
    Task RevokeUserAsync(Guid userId, DateTime now, string reason);
}
