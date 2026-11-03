using Domain.DalModels;

namespace Application.Ports.Repositories;

/// <summary>
/// Репозиторий для работы с пользователями в базе данных
/// </summary>
public interface IUserRepository
{
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
