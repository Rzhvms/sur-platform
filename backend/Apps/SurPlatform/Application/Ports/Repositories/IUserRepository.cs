using Domain.DalModels;
using Application.Models;

namespace Application.Ports.Repositories;

/// <summary>
/// Репозиторий для работы с пользователями в базе данных
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Получение данных проверки входа одним запросом
    /// </summary>
    Task<UserLoginData?> GetLoginDataAsync(string normalizedEmail);

    /// <summary>
    /// Получение данных проверки пароля пользователя
    /// </summary>
    Task<UserCredentialDal> GetCredentialAsync(Guid userId);

    /// <summary>
    /// Обновление пароля пользователя
    /// </summary>
    /// <param name="userId">Пользователь, меняющий пароль</param>
    /// <param name="password">Хеш нового пароля</param>
    /// <param name="changedAt">Время операции</param>
    /// <param name="expectedHash">Хеш пароля, который прошёл проверку</param>
    /// <returns>Признак успешной смены пароля</returns>
    Task<bool> ChangeUserPasswordAsync(Guid userId, string password, DateTime changedAt, string expectedHash);
}
