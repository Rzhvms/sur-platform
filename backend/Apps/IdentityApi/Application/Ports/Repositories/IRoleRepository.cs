using Domain.DalModels;

namespace Application.Ports.Repositories;

/// <summary>
/// Репозиторий для работы с системой ролей
/// </summary>
public interface IRoleRepository
{
    /// <summary>
    /// Получение роли по названию
    /// </summary>
    Task<RoleDal?> GetRoleByNameAsync(string name);

    /// <summary>
    /// Получение уникальных прав всех назначенных ролей пользователя
    /// </summary>
    Task<List<Guid>> GetUserRightIdListAsync(Guid userId);
}