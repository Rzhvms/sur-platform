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
    private static readonly string UserAlreadyExistsCode = ((int)Codes.UserAlreadyExists).ToString();
    private static readonly string InvalidCredentialsCode = ((int)Codes.CredentialsAreNotValid).ToString();
    private static readonly string InvalidRefreshTokenCode = ((int)Codes.RefreshTokenIsNotCorrect).ToString();
    private static readonly string InvalidAccessTokenCode = ((int)Codes.AccessTokenIsNotValid).ToString();

    /// <inheritdoc/>
    public async Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request)
    {
        var userId = await accountService.CreateAsync(request);

        if (userId is null)
        {
            return new CreateUserErrorResponse
            {
                Code = UserAlreadyExistsCode,
                Message = "Пользователь уже существует"
            };
        }

        return new CreateUserSuccessResponse
        {
            UserId = userId.Value
        };
    }

    /// <inheritdoc/>
    public async Task<ConnectTokenResponse> ConnectTokenAsync(ConnectTokenRequest request)
    {
        var userId = await accountService.AuthenticateAsync(request.Email, request.Password);

        if (userId is null)
        {
            return new ConnectTokenErrorResponse
            {
                Code = InvalidCredentialsCode,
                Message = "Неверная почта или пароль"
            };
        }

        var tokens = await sessionService.CreateAsync(userId.Value);

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
                Message = "Сессия недействительна"
            };
        }

        var value = tokens.Value;

        return new RefreshTokenSuccessResponse
        {
            AccessToken = value.AccessToken,
            RefreshToken = value.RefreshToken,
            RefreshTokenExpirationDate = value.RefreshTokenExpiresAt
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
                Message = "Сессия недействительна"
            };
        }

        return new RevocateTokenSuccessResponse
        {
            Message = "Текущая сессия завершена"
        };
    }

    /// <inheritdoc/>
    public Task<bool> IsSessionActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        return sessionService.IsActiveAsync(userId, sessionId, cancellationToken);
    }

    /// <inheritdoc/>
    public Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        return accountService.ChangePasswordAsync(userId, request.Password);
    }
}
