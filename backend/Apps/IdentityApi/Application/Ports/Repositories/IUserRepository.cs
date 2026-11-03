using Domain.DalModels;
using Application.Models;

namespace Application.Ports.Repositories;

/// <summary>
/// Репозиторий для работы с пользователями в базе данных
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Проверяет наличие профиля по нормализованной почте
    /// </summary>
    Task<bool> ExistsByEmailAsync(string normalizedEmail);

    /// <summary>
    /// Проверяет наличие парольных данных пользователя
    /// </summary>
    Task<bool> ExistsAsync(Guid userId);

    /// <summary>
    /// Получает данные проверки входа одним запросом
    /// </summary>
    Task<UserLoginData?> GetLoginDataAsync(string normalizedEmail);

    /// <summary>
    /// Получает пользователя по адресу электронной почты
    /// </summary>
    Task<UserProfileDal?> GetUserByEmailAsync(string email);

    /// <summary>
    /// Получает пользователя по уникальному идентификатору
    /// </summary>
    Task<UserProfileDal?> GetUserByUserIdAsync(Guid userId);

    /// <summary>
    /// Получает данные проверки пароля пользователя
    /// </summary>
    Task<UserCredentialDal?> GetCredentialAsync(Guid userId);

    /// <summary>
    /// Создает нового пользователя
    /// </summary>
    Task CreateUserAsync(UserProfileDal profile, UserCredentialDal credential, Guid roleId);

    /// <summary>
    /// Получение всех пользователей.
    /// </summary>
    Task<IEnumerable<UserProfileDal>> GetAllUsersAsync();

    /// <summary>
    /// Обновление пароля пользователя
    /// </summary>
    Task ChangeUserPasswordAsync(Guid userId, string password, DateTime changedAt);

    /// <summary>
    /// Удаление пользователя по идентификатору
    /// </summary>
    Task DeleteUserByIdAsync(Guid userId);
}
