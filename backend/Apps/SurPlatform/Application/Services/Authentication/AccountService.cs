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
public sealed class AccountService(IUserRepository userRepository, TimeProvider clock)
{
    /// <summary>
    /// Проверяет пароль, активность и срок временного доступа
    /// </summary>
    /// <param name="email">Логин пользователя</param>
    /// <param name="password">Предъявленный пароль</param>
    /// <returns>Проверенные учётные данные или отсутствие доступа</returns>
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
