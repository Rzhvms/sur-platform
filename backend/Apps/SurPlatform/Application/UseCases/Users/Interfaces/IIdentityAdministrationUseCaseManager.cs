using Application.Models;
using Application.UseCases.Users.Dto.Request;

namespace Application.UseCases.Users.Interfaces;

/// <summary>
/// Административные операции с пользователями, ролями и матрицей доступа
/// </summary>
public interface IIdentityAdministrationUseCaseManager
{
    /// <summary>
    /// Читает страницу пользователей
    /// </summary>
    /// <param name="after">Курсор страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Страница пользователей</returns>
    Task<IdentityUser[]> ListAsync(Guid after, int limit);

    /// <summary>
    /// Читает карточку пользователя
    /// </summary>
    /// <param name="userId">Пользователь</param>
    /// <returns>Карточка либо отсутствие записи</returns>
    Task<IdentityUser?> FindAsync(Guid userId);

    /// <summary>
    /// Читает системную матрицу
    /// </summary>
    /// <returns>Системные роли</returns>
    Task<IdentityRole[]> RolesAsync();

    /// <summary>
    /// Читает каталог прав
    /// </summary>
    /// <returns>Системные права</returns>
    Task<IdentityRight[]> RightsAsync();

    /// <summary>
    /// Синхронизирует системный каталог с версией бэкенда
    /// </summary>
    /// <param name="actor">Инициатор с действующим правом администратора</param>
    /// <param name="sessionId">Сессия инициатора</param>
    Task SynchronizeCatalogAsync(Guid actor, Guid sessionId);

    /// <summary>
    /// Сохраняет системную роль
    /// </summary>
    /// <param name="actor">Инициатор</param>
    /// <param name="sessionId">Сессия инициатора</param>
    /// <param name="roleId">Роль</param>
    /// <param name="request">Параметры изменения</param>
    /// <returns>Обновлённая роль</returns>
    Task<IdentityRole> SaveRoleAsync(Guid actor, Guid sessionId, Guid roleId, UpdateIdentityRoleRequest request);

    /// <summary>
    /// Выдаёт доступ новому пользователю
    /// </summary>
    /// <param name="actor">Инициатор</param>
    /// <param name="sessionId">Сессия инициатора</param>
    /// <param name="request">Параметры создания</param>
    /// <returns>Созданная запись</returns>
    Task<IdentityUser> CreateAsync(Guid actor, Guid sessionId, CreateIdentityUserRequest request);

    /// <summary>
    /// Сохраняет изменения пользователя
    /// </summary>
    /// <param name="actor">Инициатор</param>
    /// <param name="sessionId">Сессия инициатора</param>
    /// <param name="userId">Пользователь</param>
    /// <param name="request">Параметры изменения</param>
    /// <returns>Обновлённая запись</returns>
    Task<IdentityUser> UpdateAsync(Guid actor, Guid sessionId, Guid userId, UpdateIdentityUserRequest request);

    /// <summary>
    /// Выдаёт новый временный доступ
    /// </summary>
    /// <param name="actor">Инициатор</param>
    /// <param name="sessionId">Сессия инициатора</param>
    /// <param name="userId">Пользователь</param>
    /// <param name="activation">Параметры активации</param>
    /// <param name="version">Версия карточки</param>
    /// <param name="temporaryPassword">Новый временный пароль</param>
    /// <returns>Обновлённая карточка пользователя</returns>
    Task<IdentityUser> ReissueAsync(Guid actor, Guid sessionId, Guid userId, ActivateIdentityUserRequest? activation, Guid version, string temporaryPassword);

    /// <summary>Отключает учётную запись и связанную запись сотрудника</summary>
    /// <param name="actor">Администратор</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="userId">Отключаемая учётная запись</param>
    /// <param name="version">Ожидаемая версия карточки</param>
    /// <returns>Деактивированная карточка</returns>
    Task<IdentityUser> DeactivateAsync(Guid actor, Guid sessionId, Guid userId, Guid version);
}
