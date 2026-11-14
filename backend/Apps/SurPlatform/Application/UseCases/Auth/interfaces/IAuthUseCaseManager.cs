using Application.UseCases.Auth.Dto.Request;
using Application.UseCases.Auth.Dto.Response;
using Application.UseCases.Auth.Dto.Response.ConnectToken;
using Application.UseCases.Auth.Dto.Response.CreateUser;
using Application.UseCases.Auth.Dto.Response.RefreshToken;
using Application.UseCases.Auth.Dto.Response.RevocateToken;

namespace Application.UseCases.Auth.interfaces;

/// <summary>
/// UseCase сценарии авторизации, аутентификации, регистрации
/// </summary>
public interface IAuthUseCaseManager
{
    /// <summary>
    /// Авторизация. Получение токенов
    /// </summary>
    Task<ConnectTokenResponse> ConnectTokenAsync(ConnectTokenRequest request);

    /// <summary>
    /// Деавторизация. Отзыв токена
    /// </summary>
    Task<RevocateTokenResponse> RevocateRefreshTokenAsync(Guid userId, Guid sessionId);

    /// <summary>
    /// Проверка действительности сессии из access-токена
    /// </summary>
    Task<bool> IsSessionActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken, long accessVersion);

    /// <summary>
    /// Обновление токена
    /// </summary>
    Task<RefreshTokenResponse> RefreshTokenAsync(RefreshTokenRequest request);

    /// <summary>
    /// Отзывает браузерную сессию по refresh-токену
    /// </summary>
    /// <param name="refreshToken">Секрет текущего браузера</param>
    Task RevokeBrowserSessionAsync(string refreshToken);

    /// <summary>
    /// Проверяет сессию и право служебной операции одним чтением клиентской базы
    /// </summary>
    /// <param name="userId">Пользователь из проверенного токена</param>
    /// <param name="sessionId">Сессия пользователя</param>
    /// <param name="accessVersion">Версия доступа из токена</param>
    /// <param name="rightCode">Право операции</param>
    /// <param name="cancellationToken">Отмена проверки</param>
    /// <returns>Разрешение выполнить операцию</returns>
    Task<bool> CheckSessionRightAsync(Guid userId, Guid sessionId, long accessVersion, string rightCode,
        CancellationToken cancellationToken);
}
