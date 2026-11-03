using System.Security.Cryptography;
using System.Text;
using Application.Constants;
using Application.Models;
using Application.Ports.Repositories;
using Application.Ports.Services.Encryption;
using Application.Ports.Services.Jwt;
using Application.UseCases.Auth.Dto.Request;
using Application.UseCases.Auth.Dto.Response.ConnectToken;
using Application.UseCases.Auth.Dto.Response.CreateUser;
using Application.UseCases.Auth.Dto.Response.RefreshToken;
using Application.UseCases.Auth.Dto.Response.RevocateToken;
using Application.UseCases.Auth.interfaces;
using Core.Abstractions.Exceptions;
using Domain.DalModels;
using Codes = Application.ErrorCodes.ErrorCodes;

namespace Application.UseCases.Auth;

/// <inheritdoc />
public sealed class AuthUseCaseManager(
    IPasswordEncryptionService passwords,
    IUserRepository users,
    IRoleRepository roles,
    ISessionRepository sessions,
    IJwtGenerationService jwt,
    TimeProvider clock) : IAuthUseCaseManager
{
    /// <inheritdoc/>
    public async Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request)
    {

        var email = request.Email.Trim().ToLowerInvariant();
        if (await users.GetUserByEmailAsync(email) is not null)
            return new CreateUserErrorResponse { Code = ((int)Codes.UserAlreadyExists).ToString(), Message = "Пользователь уже существует" };
        var role = await roles.GetRoleByNameAsync(request.RoleName)
            ?? throw new EntityNotFoundException("Роль не найдена");
        var id = Guid.NewGuid();
        var now = clock.GetUtcNow().UtcDateTime;
        await users.CreateUserAsync(new UserProfileDal
        {
            Id = Guid.NewGuid(), UserId = id, Email = request.Email.Trim(), NormalizedEmail = email,
            FirstName = request.FirstName, LastName = request.LastName, Patronymic = request.Patronymic,
            CreatedAt = now, UpdatedAt = now
        }, new UserCredentialDal
        {
            Id = Guid.NewGuid(), UserId = id,
            Password = passwords.HashPassword(request.Password, passwords.GenerateSalt()), PasswordChangedAt = now
        }, role.Id);

        return new CreateUserSuccessResponse { UserId = id };
    }

    /// <inheritdoc/>
    public async Task<ConnectTokenResponse> ConnectTokenAsync(ConnectTokenRequest request)
    {

        var user = await users.GetUserByEmailAsync(request.Email.Trim().ToLowerInvariant());
        var credential = user is null ? null : await users.GetCredentialAsync(user.UserId);
        var now = clock.GetUtcNow().UtcDateTime;
        if (user is null || credential is null || !user.IsActive || user.LockoutUntil > now ||
            !passwords.VerifyPassword(request.Password, credential.Password))
            return new ConnectTokenErrorResponse { Code = ((int)Codes.CredentialsAreNotValid).ToString(), Message = "Неверная почта или пароль" };

        var expires = now.AddMinutes(jwt.GetRefreshTokenLifetimeInMinutes());
        var session = new SessionDal
        {
            Id = Guid.NewGuid(), UserId = user.UserId, CreatedAt = now, UpdatedAt = now, ExpiresAt = expires
        };
        var refresh = jwt.GenerateRefreshToken();
        await sessions.CreateAsync(session, new RefreshTokenDal
        {
            Id = Guid.NewGuid(), SessionId = session.Id, TokenHash = HashRefresh(refresh), CreatedAt = now, ExpiresAt = expires
        });
        var access = await GenerateAccessAsync(session);

        return new ConnectTokenSuccessResponse { AccessToken = access, RefreshToken = refresh };
    }

    /// <inheritdoc/>
    public async Task<RefreshTokenResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {

        var token = await sessions.GetRefreshAsync(HashRefresh(request.RefreshToken));
        var now = clock.GetUtcNow().UtcDateTime;
        if (token is null)
            return InvalidRefresh();
        var session = await sessions.GetAsync(token.SessionId);
        if (session is null || session.RevokedAt is not null || session.ExpiresAt <= now)
            return InvalidRefresh();
        if (token.RevokedAt is not null)
        {
            await sessions.RevokeAsync(session.Id, now, SessionRevokeReasons.RefreshReuse);

            return InvalidRefresh();
        }
        var user = await users.GetUserByUserIdAsync(session.UserId);
        if (token.ExpiresAt <= now || user is not { IsActive: true })
            return InvalidRefresh();

        var raw = jwt.GenerateRefreshToken();
        var expiration = now.AddMinutes(jwt.GetRefreshTokenLifetimeInMinutes());
        if (expiration > session.ExpiresAt) expiration = session.ExpiresAt;
        if (!await sessions.RotateAsync(token.Id, new RefreshTokenDal
        {
            Id = Guid.NewGuid(), SessionId = session.Id, TokenHash = HashRefresh(raw), CreatedAt = now, ExpiresAt = expiration
        }, now))
        {
            return InvalidRefresh();
        }
        var access = await GenerateAccessAsync(session);

        return new RefreshTokenSuccessResponse { AccessToken = access, RefreshToken = raw, RefreshTokenExpirationDate = expiration };
    }

    /// <inheritdoc/>
    public async Task<RevocateTokenResponse> RevocateRefreshTokenAsync(Guid userId)
    {

        await sessions.RevokeUserAsync(userId, clock.GetUtcNow().UtcDateTime, SessionRevokeReasons.Logout);

        return new RevocateTokenSuccessResponse { Message = "Сессии завершены" };
    }

    /// <inheritdoc/>
    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {

        if (await users.GetCredentialAsync(userId) is null)
            throw new EntityNotFoundException("Пользователь не найден");
        var now = clock.GetUtcNow().UtcDateTime;
        await users.ChangeUserPasswordAsync(userId, passwords.HashPassword(request.Password, passwords.GenerateSalt()), now);

    }

    /// <summary>
    /// Выпускает access-токен с правами всех ролей пользователя
    /// </summary>
    private async Task<string> GenerateAccessAsync(SessionDal session) => jwt.GenerateAccessToken(new AccessTokenClaims
    {
        UserId = session.UserId, SessionId = session.Id, RightIdList = await roles.GetUserRightIdsAsync(session.UserId)
    });

    /// <summary>
    /// Вычисляет хеш случайного refresh-токена для хранения в базе
    /// </summary>
    private static string HashRefresh(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>
    /// Создаёт общий ответ недействительной сессии
    /// </summary>
    private static RefreshTokenErrorResponse InvalidRefresh() => new()
    {
        Code = ((int)Codes.RefreshTokenIsNotCorrect).ToString(), Message = "Сессия недействительна"
    };
}
