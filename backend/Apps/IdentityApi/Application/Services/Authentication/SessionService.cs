using Application.Constants;
using Application.Helpers.Jwt;
using Application.Models;
using Application.Ports.Repositories;
using Domain.DalModels;

namespace Application.Services.Authentication;

/// <summary>
/// Сервис для управления авторизационными сессиями
/// </summary>
public sealed class SessionService(ISessionRepository sessionRepository, IRoleRepository roleRepository)
{
    /// <summary>
    /// Создание сессии и токенов для нового входа
    /// </summary>
    public async Task<SessionTokens> CreateAsync(Guid userId)
    {
        var currentDateTime = DateTime.UtcNow;
        var session = new SessionDal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = currentDateTime,
            UpdatedAt = currentDateTime,
            ExpiresAt = currentDateTime.AddMinutes(JwtGenerationHelper.GetRefreshTokenLifetimeInMinutes())
        };
        
        var refreshToken = JwtGenerationHelper.GenerateRefreshToken();
        var accessToken = await GenerateAccessAsync(userId, session.Id, session.ExpiresAt);
        
        var token = CreateRefreshTokenModel(session.Id, refreshToken, currentDateTime, session.ExpiresAt);
        await sessionRepository.CreateAsync(session, token);

        return new SessionTokens(accessToken, refreshToken, session.ExpiresAt);
    }

    /// <summary>
    /// Обновляет токены действующей сессии и отзывает ее при повторном использовании refresh-токена
    /// </summary>
    public async Task<SessionTokens?> RefreshAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var tokenHash = JwtGenerationHelper.HashRefreshToken(refreshToken);
        var session = await sessionRepository.GetRefreshSessionAsync(tokenHash);
        var currentDateTime = DateTime.UtcNow;

        if (session is null || session.SessionRevokedAt is not null || session.SessionExpiresAt <= currentDateTime)
        {
            return null;
        }

        if (session.TokenRevokedAt is not null)
        {
            await sessionRepository.RevokeAsync(
                session.SessionId, session.UserId, 
                currentDateTime, SessionRevokeReasons.RefreshReuse);
            
            return null;
        }

        if (session.TokenExpiresAt <= currentDateTime || !session.IsUserActive)
        {
            return null;
        }

        var expiresAt = currentDateTime.AddMinutes(JwtGenerationHelper.GetRefreshTokenLifetimeInMinutes());

        if (expiresAt > session.SessionExpiresAt)
        {
            expiresAt = session.SessionExpiresAt;
        }

        var nextRefreshToken = JwtGenerationHelper.GenerateRefreshToken();
        var accessToken = await GenerateAccessAsync(session.UserId, session.SessionId, session.SessionExpiresAt);
        var nextToken = CreateRefreshTokenModel(session.SessionId, nextRefreshToken, currentDateTime, expiresAt);

        var rotated = await sessionRepository.RotateAsync(session.TokenId, nextToken, currentDateTime);
        return rotated ? new SessionTokens(accessToken, nextRefreshToken, expiresAt) : null;
    }

    /// <summary>
    /// Завершает только указанную сессию пользователя
    /// </summary>
    public async Task<bool> RevokeAsync(Guid userId, Guid sessionId)
    {
        return await sessionRepository.RevokeAsync(sessionId, userId, DateTime.UtcNow, SessionRevokeReasons.Logout);
    }

    /// <summary>
    /// Проверяет принадлежность и действительность сессии пользователя
    /// </summary>
    public async Task<bool> IsActiveAsync(Guid userId, Guid sessionId)
    {
        return await sessionRepository.IsActiveAsync(userId, sessionId, DateTime.UtcNow);
    }

    /// <summary>
    /// Генерация access-токен с правами всех ролей пользователя
    /// </summary>
    private async Task<string> GenerateAccessAsync(Guid userId, Guid sessionId, DateTime sessionExpiresAt)
    {
        var rightIdList = await roleRepository.GetUserRightIdListAsync(userId);
        var claims = new AccessTokenClaims
        {
            UserId = userId,
            SessionId = sessionId,
            RightIdList = rightIdList
        };

        return JwtGenerationHelper.GenerateAccessToken(claims, sessionExpiresAt);
    }

    /// <summary>
    /// Формирование модели refresh-токена
    /// </summary>
    private RefreshTokenDal CreateRefreshTokenModel(Guid sessionId, string refreshToken, DateTime currentDateTime, DateTime expiresAt)
    {
        return new RefreshTokenDal
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            TokenHash = JwtGenerationHelper.HashRefreshToken(refreshToken),
            CreatedAt = currentDateTime,
            ExpiresAt = expiresAt
        };
    }
}