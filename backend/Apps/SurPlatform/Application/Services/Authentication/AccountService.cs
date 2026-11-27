using Application.Services.Authentication.Interfaces;
using Application.Constants;
using Application.Helpers.Encryption;
using Application.Models;
using Application.Ports.Repositories;

namespace Application.Services.Authentication;

/// <summary>
/// Проверяет учётные данные перед открытием сессии
/// </summary>
/// <param name="userRepository">Источник текущих учётных данных</param>
/// <param name="clock">Источник времени проверки</param>
internal sealed class AccountService(IUserRepository userRepository, TimeProvider clock) : IAccountService
{
    /// <inheritdoc/>
    public async Task<UserLoginData?> AuthenticateAsync(string email, string password)
    {
        var user = await userRepository.GetLoginDataAsync(email.Trim().ToLowerInvariant());
        var now = clock.GetUtcNow().UtcDateTime;
        if (user is null || user.UserId == IdentityAccounts.SystemUserId || !user.IsActive ||
            user.LockoutUntil > now || user.MustChangePassword &&
                (user.TemporaryPasswordExpiresAt is null || user.TemporaryPasswordExpiresAt <= now) ||
            !PasswordEncryptionHelper.VerifyPassword(password, user.Password))
        {
            return null;
        }

        return user;
    }
}
