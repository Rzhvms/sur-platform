using Domain.DalModels;
using Application.Models;

namespace Application.Ports.Repositories;

/// <summary>
/// Репозиторий для работы с системой ролей
/// </summary>
public interface IRoleRepository
{
    /// <summary>
    /// Получение уникальных прав всех назначенных ролей пользователя
    /// </summary>
    /// <param name="userId">Пользователь из выбранной клиентской базы</param>
    /// <returns>Согласованный снимок прав и версии доступа или отсутствие активной записи</returns>
    Task<IdentityAccess?> GetAccessAsync(Guid userId);
}
