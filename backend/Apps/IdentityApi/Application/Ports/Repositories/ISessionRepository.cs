using Domain.DalModels;

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
    /// Находит refresh-токен по хешу
    /// </summary>
    Task<RefreshTokenDal?> GetRefreshAsync(string hash);

    /// <summary>
    /// Однократно заменяет действующий токен и обновляет время активности сессии
    /// </summary>
    Task<bool> RotateAsync(Guid tokenId, RefreshTokenDal next, DateTime now);

    /// <summary>
    /// Завершает сессию и отзывает её токены
    /// </summary>
    Task RevokeAsync(Guid sessionId, DateTime now, string reason);

    /// <summary>
    /// Завершает все сессии пользователя
    /// </summary>
    Task RevokeUserAsync(Guid userId, DateTime now, string reason);
}
