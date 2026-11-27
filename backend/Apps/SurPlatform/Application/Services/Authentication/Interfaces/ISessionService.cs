using Application.Models;

namespace Application.Services.Authentication.Interfaces;

/// <summary>
/// Создание, обновление, проверка и отзыв пользовательских сессий
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Создание сессии и токенов для нового входа
    /// </summary>
    /// <param name="userId">Пользователь с проверенным паролем</param>
    /// <param name="accessVersion">Версия доступа во время проверки пароля</param>
    /// <returns>Токены сессии или отказ при изменении доступа</returns>
    Task<SessionTokens?> CreateAsync(Guid userId, long accessVersion);

    /// <summary>
    /// Обновляет токены действующей сессии и отзывает ее при повторном использовании refresh-токена
    /// </summary>
    /// <param name="refreshToken">Предъявленный refresh-токен текущего клиента</param>
    /// <returns>Новая пара токенов или отсутствие действующей сессии</returns>
    Task<SessionTokens?> RefreshAsync(string refreshToken);

    /// <summary>
    /// Завершает текущую сессию по refresh-токену данного клиента
    /// </summary>
    /// <param name="refreshToken">Секрет браузерной cookie</param>
    Task RevokeRefreshAsync(string refreshToken);

    /// <summary>
    /// Завершает только указанную сессию пользователя
    /// </summary>
    /// <param name="userId">Пользователь, которому принадлежит сессия</param>
    /// <param name="sessionId">Идентификатор отзываемой сессии</param>
    /// <returns>Признак найденной и отозванной сессии</returns>
    Task<bool> RevokeAsync(Guid userId, Guid sessionId);

    /// <summary>
    /// Проверяет принадлежность и действительность сессии пользователя
    /// </summary>
    /// <param name="userId">Пользователь, которому должна принадлежать сессия</param>
    /// <param name="sessionId">Проверяемая сессия</param>
    /// <param name="cancellationToken">Отмена проверки сессии</param>
    /// <param name="accessVersion">Версия доступа из JWT</param>
    /// <param name="rightCode">Дополнительное право для проверки служебной операции</param>
    /// <returns>Признак действующей сессии</returns>
    Task<bool> IsActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken, long accessVersion, string? rightCode = null);
}
