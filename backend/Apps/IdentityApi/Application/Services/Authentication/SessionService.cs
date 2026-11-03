using Application.Constants;
using Application.Models;
using Application.Ports.Repositories;
using Application.Ports.Services.Jwt;
using Domain.DalModels;

namespace Application.Services.Authentication;

/// <summary>
/// Создание, обновление и проверка авторизационных сессий
/// </summary>
public sealed class SessionService(
    ISessionRepository sessionRepository,
    IRoleRepository roleRepository,
    IJwtGenerationService jwtGenerationService,
    TimeProvider clock)
{
    /// <summary>
    /// Создаёт отдельную сессию и пару токенов для нового входа
    /// </summary>
    public async Task<SessionTokens> CreateAsync(Guid userId)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var session = new SessionDal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = now.AddMinutes(jwtGenerationService.GetRefreshTokenLifetimeInMinutes())
        };
        var refreshToken = jwtGenerationService.GenerateRefreshToken();
        var accessToken = await GenerateAccessAsync(userId, session.Id, session.ExpiresAt);
        var token = CreateRefreshToken(session.Id, refreshToken, now, session.ExpiresAt);

        await sessionRepository.CreateAsync(session, token);

        return new SessionTokens(accessToken, refreshToken, session.ExpiresAt);
    }

    /// <summary>
    /// Обновляет токены действующей сессии и отзывает её при повторном использовании refresh-токена
    /// </summary>
    public async Task<SessionTokens?> RefreshAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var tokenHash = jwtGenerationService.HashRefreshToken(refreshToken);
        var session = await sessionRepository.GetRefreshSessionAsync(tokenHash);
        var now = clock.GetUtcNow().UtcDateTime;

        if (session is null || session.SessionRevokedAt is not null || session.SessionExpiresAt <= now)
        {
            return null;
        }

        if (session.TokenRevokedAt is not null)
        {
            await sessionRepository.RevokeAsync(session.SessionId, session.UserId, now, SessionRevokeReasons.RefreshReuse);
            return null;
        }

        if (session.TokenExpiresAt <= now || !session.IsUserActive)
        {
            return null;
        }

        var expiresAt = now.AddMinutes(jwtGenerationService.GetRefreshTokenLifetimeInMinutes());

        if (expiresAt > session.SessionExpiresAt)
        {
            expiresAt = session.SessionExpiresAt;
        }

        var nextRefreshToken = jwtGenerationService.GenerateRefreshToken();
        var accessToken = await GenerateAccessAsync(session.UserId, session.SessionId, session.SessionExpiresAt);
        var nextToken = CreateRefreshToken(session.SessionId, nextRefreshToken, now, expiresAt);

        if (!await sessionRepository.RotateAsync(session.TokenId, nextToken, now))
        {
            return null;
        }

        return new SessionTokens(accessToken, nextRefreshToken, expiresAt);
    }

    /// <summary>
    /// Завершает только указанную сессию пользователя
    /// </summary>
    public Task<bool> RevokeAsync(Guid userId, Guid sessionId)
    {
        return sessionRepository.RevokeAsync(sessionId, userId, clock.GetUtcNow().UtcDateTime, SessionRevokeReasons.Logout);
    }

    /// <summary>
    /// Проверяет принадлежность и действительность сессии пользователя
    /// </summary>
    public Task<bool> IsActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        return sessionRepository.IsActiveAsync(userId, sessionId, clock.GetUtcNow().UtcDateTime, cancellationToken);
    }

    /// <summary>
    /// Выпускает access-токен с правами всех ролей пользователя
    /// </summary>
    private async Task<string> GenerateAccessAsync(Guid userId, Guid sessionId, DateTime sessionExpiresAt)
    {
        var rightIdList = await roleRepository.GetUserRightIdsAsync(userId);
        var claims = new AccessTokenClaims
        {
            UserId = userId,
            SessionId = sessionId,
            RightIdList = rightIdList
        };

        return jwtGenerationService.GenerateAccessToken(claims, sessionExpiresAt);
    }

    /// <summary>
    /// Формирует запись refresh-токена без хранения его исходного значения
    /// </summary>
    private RefreshTokenDal CreateRefreshToken(Guid sessionId, string refreshToken, DateTime now, DateTime expiresAt)
    {
        return new RefreshTokenDal
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            TokenHash = jwtGenerationService.HashRefreshToken(refreshToken),
            CreatedAt = now,
            ExpiresAt = expiresAt
        };
    }
}
