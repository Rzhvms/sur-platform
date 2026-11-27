using Application.Models;

namespace Application.Services.Authentication.Interfaces;

/// <summary>
/// Проверка учетных данных перед открытием сессии
/// </summary>
public interface IAccountService
{
    /// <summary>
    /// Проверяет пароль, активность и срок временного доступа
    /// </summary>
    /// <param name="email">Логин пользователя</param>
    /// <param name="password">Предъявленный пароль</param>
    /// <returns>Проверенные учётные данные или отсутствие доступа</returns>
    Task<UserLoginData?> AuthenticateAsync(string email, string password);
}
