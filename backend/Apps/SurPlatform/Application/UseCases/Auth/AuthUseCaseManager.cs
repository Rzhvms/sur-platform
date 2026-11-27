using Application.Constants;
using Application.Services.Authentication.Interfaces;
using Application.UseCases.Auth.Dto.Request;
using Application.UseCases.Auth.Dto.Response.ConnectToken;
using Application.UseCases.Auth.Dto.Response.CreateUser;
using Application.UseCases.Auth.Dto.Response.RefreshToken;
using Application.UseCases.Auth.Dto.Response.RevocateToken;
using Application.UseCases.Auth.interfaces;
using Codes = Application.ErrorCodes.ErrorCodes;

namespace Application.UseCases.Auth;

/// <inheritdoc />
internal sealed class AuthUseCaseManager(IAccountService accountService, ISessionService sessionService) : IAuthUseCaseManager
{
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
                Message = IdentityMessages.InvalidCredentials
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
    public Task RevokeBrowserSessionAsync(string refreshToken)
    {
        return sessionService.RevokeRefreshAsync(refreshToken);
    }
}
