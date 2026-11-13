using Application.Constants;
using Application.Helpers.Jwt;
using Application.Models;
using Application.Ports.Repositories;
using Application.Settings;
using Domain.DalModels;
using Microsoft.IdentityModel.Tokens;

namespace Application.Services.Authentication;

/// <summary>
/// Сервис для управления авторизационными сессиями
/// </summary>
/// <param name="sessionRepository">Хранилище сессий и refresh-токенов</param>
/// <param name="roleRepository">Источник действующих прав пользователя</param>
/// <param name="jwtSettings">Настройки выпуска токенов</param>
/// <param name="signingKey">Ключ подписи access-токенов</param>
/// <param name="clock">Источник времени для проверки и выпуска токенов</param>
public sealed class SessionService(
    ISessionRepository sessionRepository,
    IRoleRepository roleRepository,
    JwtSettings jwtSettings,
    RsaSecurityKey signingKey,
    TimeProvider clock)
{
    /// <summary>
    /// Граница между issuer и исходным секретом при хешировании
    /// </summary>
    private const string RefreshHashSeparator = "\n";

    /// <summary>
    /// Создание сессии и токенов для нового входа
    /// </summary>
    /// <param name="userId">Пользователь с проверенным паролем</param>
    /// <param name="accessVersion">Версия доступа во время проверки пароля</param>
    /// <returns>Токены сессии или отказ при изменении доступа</returns>
    public async Task<SessionTokens?> CreateAsync(Guid userId, long accessVersion)
    {
        var currentDateTime = clock.GetUtcNow().UtcDateTime;
        var access = await roleRepository.GetAccessAsync(userId);
        if (access is null || access.AccessVersion != accessVersion)
        {
            return null;
        }

        var session = new SessionDal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = currentDateTime,
            UpdatedAt = currentDateTime,
            ExpiresAt = currentDateTime.AddDays(jwtSettings.SessionLifetimeDays)
        };

        var refreshToken = JwtGenerationHelper.GenerateRefreshToken(jwtSettings);
        var tokenExpiresAt = access.MustChangePassword && access.TemporaryPasswordExpiresAt is { } deadline &&
            deadline < session.ExpiresAt ? deadline : session.ExpiresAt;
        if (tokenExpiresAt <= currentDateTime.AddSeconds(1))
        {
            return null;
        }

        var accessToken = GenerateAccess(userId, session.Id, tokenExpiresAt, access);

        var refreshExpiresAt = currentDateTime.AddMinutes(JwtGenerationHelper.GetRefreshTokenLifetimeInMinutes(jwtSettings));
        if (refreshExpiresAt > session.ExpiresAt)
        {
            refreshExpiresAt = session.ExpiresAt;
        }

        var token = CreateRefreshTokenModel(session.Id, refreshToken, currentDateTime, refreshExpiresAt);
        return await sessionRepository.CreateAsync(session, token, accessVersion)
            ? new SessionTokens(accessToken, refreshToken, refreshExpiresAt)
            : null;
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

        var tokenHash = HashRefreshToken(refreshToken);
        var session = await sessionRepository.GetRefreshSessionAsync(tokenHash);
        var currentDateTime = clock.GetUtcNow().UtcDateTime;

        if (session is null || session.SessionRevokedAt is not null || session.SessionExpiresAt <= currentDateTime)
        {
            return null;
        }

        if (session.UsedAt is not null)
        {
            await sessionRepository.RevokeAsync(
                session.SessionId, session.UserId,
                currentDateTime, SessionRevokeReasons.RefreshReuse);

            return null;
        }

        if (session.TokenRevokedAt is not null || session.TokenExpiresAt <= currentDateTime || !session.IsUserActive)
        {
            return null;
        }

        var expiresAt = currentDateTime.AddMinutes(JwtGenerationHelper.GetRefreshTokenLifetimeInMinutes(jwtSettings));

        if (expiresAt > session.SessionExpiresAt)
        {
            expiresAt = session.SessionExpiresAt;
        }

        var access = await roleRepository.GetAccessAsync(session.UserId);
        if (access is null || access.MustChangePassword)
        {
            return null;
        }

        var nextRefreshToken = JwtGenerationHelper.GenerateRefreshToken(jwtSettings);
        if (session.SessionExpiresAt <= clock.GetUtcNow().UtcDateTime.AddSeconds(1))
        {
            return null;
        }

        var accessToken = GenerateAccess(session.UserId, session.SessionId, session.SessionExpiresAt, access);
        var nextToken = CreateRefreshTokenModel(session.SessionId, nextRefreshToken, currentDateTime, expiresAt);

        var rotated = await sessionRepository.RotateAsync(session.TokenId, nextToken, currentDateTime, access.AccessVersion);
        return rotated ? new SessionTokens(accessToken, nextRefreshToken, expiresAt) : null;
    }

    /// <summary>
    /// Завершает текущую сессию по refresh-токену данного клиента
    /// </summary>
    /// <param name="refreshToken">Секрет браузерной cookie</param>
    public async Task RevokeRefreshAsync(string refreshToken)
    {
        var session = await sessionRepository.GetRefreshSessionAsync(HashRefreshToken(refreshToken));
        if (session is not null && session.SessionRevokedAt is null &&
            session.TokenRevokedAt is null && session.UsedAt is null &&
            session.TokenExpiresAt > clock.GetUtcNow().UtcDateTime)
        {
            await RevokeAsync(session.UserId, session.SessionId);
        }
    }

    /// <summary>
    /// Завершает только указанную сессию пользователя
    /// </summary>
    public async Task<bool> RevokeAsync(Guid userId, Guid sessionId)
    {
        return await sessionRepository.RevokeAsync(sessionId, userId, clock.GetUtcNow().UtcDateTime, SessionRevokeReasons.Logout);
    }

    /// <summary>
    /// Проверяет принадлежность и действительность сессии пользователя
    /// </summary>
    /// <param name="userId">Пользователь, которому должна принадлежать сессия</param>
    /// <param name="sessionId">Проверяемая сессия</param>
    /// <param name="cancellationToken">Отмена проверки сессии</param>
    /// <param name="accessVersion">Версия доступа из JWT</param>
    /// <param name="rightCode">Дополнительное право для проверки служебной операции</param>
    /// <returns>Признак действующей сессии</returns>
    public async Task<bool> IsActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken, long accessVersion, string? rightCode = null)
    {
        return await sessionRepository.IsActiveAsync(userId, sessionId, clock.GetUtcNow().UtcDateTime, cancellationToken, accessVersion, rightCode);
    }

    /// <summary>
    /// Генерация access-токен с правами всех ролей пользователя
    /// </summary>
    /// <param name="userId">Владелец сессии</param>
    /// <param name="sessionId">Сессия для токена</param>
    /// <param name="sessionExpiresAt">Верхняя граница срока сессии</param>
    /// <param name="access">Согласованный снимок доступа</param>
    /// <returns>Подписанный JWT</returns>
    private string GenerateAccess(Guid userId, Guid sessionId, DateTime sessionExpiresAt, IdentityAccess access)
    {
        var claims = new AccessTokenClaims
        {
            UserId = userId,
            SessionId = sessionId,
            RightIdList = access.MustChangePassword ? [] : access.RightIds,
            AccessVersion = access.AccessVersion,
            MustChangePassword = access.MustChangePassword
        };

        // Исторические колонки PostgreSQL timestamp хранят UTC без признака DateTimeKind
        return JwtGenerationHelper.GenerateAccessToken(
            claims, DateTime.SpecifyKind(sessionExpiresAt, DateTimeKind.Utc), jwtSettings, signingKey,
            clock.GetUtcNow().UtcDateTime);
    }

    /// <summary>
    /// Привязывает сохранённый хеш refresh-токена к издателю клиента
    /// </summary>
    /// <param name="refreshToken">Исходное значение токена сессии</param>
    /// <returns>Хеш, пригодный только для текущего клиентского хоста</returns>
    private string HashRefreshToken(string refreshToken)
    {
        return JwtGenerationHelper.HashRefreshToken(string.Concat(jwtSettings.AccessTokenSettings.Issuer, RefreshHashSeparator, refreshToken));
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
            TokenHash = HashRefreshToken(refreshToken),
            CreatedAt = currentDateTime,
            ExpiresAt = expiresAt
        };
    }
}
