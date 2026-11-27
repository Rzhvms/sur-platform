using Application.Constants;
using Application.Services.Authentication;
using Application.UseCases.Auth.Dto.Request;
using Application.UseCases.Auth.Dto.Response.ConnectToken;
using Application.UseCases.Auth.Dto.Response.CreateUser;
using Application.UseCases.Auth.Dto.Response.RefreshToken;
using Application.UseCases.Auth.Dto.Response.RevocateToken;
using Application.UseCases.Auth.interfaces;
using Codes = Application.ErrorCodes.ErrorCodes;

namespace Application.UseCases.Auth;

/// <inheritdoc />
public sealed class AuthUseCaseManager(AccountService accountService, SessionService sessionService) : IAuthUseCaseManager
{
    /// <summary>
    /// Invalid credentials
    /// </summary>
    private const string InvalidCredentials = "Неверная почта или пароль";

    /// <summary>
    /// Session revoked
    /// </summary>
    private const string SessionRevoked = "Текущая сессия завершена";


    private static readonly string InvalidCredentialsCode = ((int)Codes.CredentialsAreNotValid).ToString();
    private static readonly string InvalidRefreshTokenCode = ((int)Codes.RefreshTokenIsNotCorrect).ToString();
    private static readonly string InvalidAccessTokenCode = ((int)Codes.AccessTokenIsNotValid).ToString();

    /// <inheritdoc/>
    public async Task<ConnectTokenResponse> ConnectTokenAsync(ConnectTokenRequest request)
    {
        var user = await accountService.AuthenticateAsync(request.Email, request.Password);
        var tokens = user is null ? null : await sessionService.CreateAsync(user.UserId, user.AccessVersion);

        if (tokens is null)
        {
            return new ConnectTokenErrorResponse
            {
                Code = InvalidCredentialsCode,
                Message = InvalidCredentials
            };
        }

        return new ConnectTokenSuccessResponse
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken
        };
    }

    /// <inheritdoc/>
    public async Task<RefreshTokenResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var tokens = await sessionService.RefreshAsync(request.RefreshToken);

        if (tokens is null)
        {
            return new RefreshTokenErrorResponse
            {
                Code = InvalidRefreshTokenCode,
                Message = IdentityMessages.InvalidSession
            };
        }

        return new RefreshTokenSuccessResponse
        {
            AccessToken = tokens.AccessToken,
            RefreshToken = tokens.RefreshToken,
            RefreshTokenExpirationDate = tokens.RefreshTokenExpiresAt
        };
    }

    /// <inheritdoc/>
    public async Task<RevocateTokenResponse> RevocateRefreshTokenAsync(Guid userId, Guid sessionId)
    {
        if (!await sessionService.RevokeAsync(userId, sessionId))
        {
            return new RevocateTokenErrorResponse
            {
                Code = InvalidAccessTokenCode,
                Message = IdentityMessages.InvalidSession
            };
        }

        return new RevocateTokenSuccessResponse
        {
            Message = SessionRevoked
        };
    }

    /// <inheritdoc/>
    public Task<bool> IsSessionActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken, long accessVersion)
    {
        return sessionService.IsActiveAsync(userId, sessionId, cancellationToken, accessVersion);
    }

    /// <inheritdoc/>
    public Task RevokeBrowserSessionAsync(string refreshToken)
    {
        return sessionService.RevokeRefreshAsync(refreshToken);
    }

    /// <inheritdoc/>
    public Task<bool> CheckSessionRightAsync(Guid userId, Guid sessionId, long accessVersion, string rightCode,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || sessionId == Guid.Empty || accessVersion < 1 || string.IsNullOrWhiteSpace(rightCode))
        {
            return Task.FromResult(false);
        }

        return sessionService.IsActiveAsync(userId, sessionId, cancellationToken, accessVersion, rightCode);
    }
}
