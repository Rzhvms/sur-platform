using Domain.DalModels;
using Application.Models;

namespace Application.Ports.Repositories;

/// <summary>
/// Репозиторий для работы с пользователями в базе данных
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Проверка наличия профиля по нормализованной почте
    /// </summary>
    Task<bool> ExistsByEmailAsync(string normalizedEmail);

    /// <summary>
    /// Проверка наличия парольных данных пользователя
    /// </summary>
    Task<bool> IsExistAsync(Guid userId);

    /// <summary>
    /// Получение данных проверки входа одним запросом
    /// </summary>
    Task<UserLoginData?> GetLoginDataAsync(string normalizedEmail);

    /// <summary>
    /// Получение пользователя по адресу электронной почты
    /// </summary>
    Task<UserProfileDal?> GetUserByEmailAsync(string email);

    /// <summary>
    /// Получение пользователя по уникальному идентификатору
    /// </summary>
    Task<UserProfileDal> GetUserByUserIdAsync(Guid userId);

    /// <summary>
    /// Получение данных проверки пароля пользователя
    /// </summary>
    Task<UserCredentialDal> GetCredentialAsync(Guid userId);

    /// <summary>
    /// Создание нового пользователя
    /// </summary>
    Task CreateUserAsync(UserProfileDal profile, UserCredentialDal credential, Guid roleId);

    /// <summary>
    /// Получение всех пользователей
    /// </summary>
    Task<IEnumerable<UserProfileDal>> GetUserProfilesAsync();

    /// <summary>
    /// Обновление пароля пользователя
    /// </summary>
    Task ChangeUserPasswordAsync(Guid userId, string password, DateTime changedAt);

    /// <summary>
    /// Удаление пользователя по идентификатору
    /// </summary>
    Task DeleteUserByIdAsync(Guid userId);
}