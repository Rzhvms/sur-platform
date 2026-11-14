using Application.Models;
using Application.Ports.Repositories;
using Application.Services.Authentication;
using Application.UseCases.Users.Dto.Request;

namespace Application.UseCases.Users;

/// <summary>
/// Координирует административные запросы пользователей и ролей
/// </summary>
/// <param name="repository">Хранилище выбранного клиента</param>
/// <param name="invitations">Сервис выдачи временного доступа</param>
public sealed class IdentityAdministrationUseCaseManager(
    IIdentityAdministrationRepository repository, IdentityInvitationService invitations)
{
    /// <summary>
    /// Читает страницу пользователей
    /// </summary>
    /// <param name="after">Курсор страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Страница пользователей</returns>
    public Task<IdentityUser[]> ListAsync(Guid after, int limit)
    {
        return repository.ListAsync(after, limit);
    }

    /// <summary>
    /// Читает карточку пользователя
    /// </summary>
    /// <param name="userId">Пользователь</param>
    /// <returns>Карточка либо отсутствие записи</returns>
    public Task<IdentityUser?> FindAsync(Guid userId)
    {
        return repository.FindAsync(userId);
    }

    /// <summary>
    /// Читает системную матрицу
    /// </summary>
    /// <returns>Системные роли</returns>
    public Task<IdentityRole[]> RolesAsync()
    {
        return repository.RolesAsync();
    }

    /// <summary>
    /// Читает каталог прав
    /// </summary>
    /// <returns>Системные права</returns>
    public Task<IdentityRight[]> RightsAsync()
    {
        return repository.RightsAsync();
    }

    /// <summary>
    /// Синхронизирует системный каталог с версией бэкенда
    /// </summary>
    /// <param name="actor">Инициатор с действующим правом администратора</param>
    /// <param name="sessionId">Сессия инициатора</param>
    public Task SynchronizeCatalogAsync(Guid actor, Guid sessionId)
    {
        return repository.SynchronizeCatalogAsync(actor, sessionId);
    }

    /// <summary>
    /// Сохраняет системную роль
    /// </summary>
    /// <param name="actor">Инициатор</param>
    /// <param name="sessionId">Сессия инициатора</param>
    /// <param name="roleId">Роль</param>
    /// <param name="request">Параметры изменения</param>
    /// <returns>Обновлённая роль</returns>
    public Task<IdentityRole> SaveRoleAsync(Guid actor, Guid sessionId, Guid roleId, UpdateIdentityRoleRequest request)
    {
        return repository.SaveRoleAsync(actor, sessionId, roleId, request);
    }

    /// <summary>
    /// Выдаёт доступ новому пользователю
    /// </summary>
    /// <param name="actor">Инициатор</param>
    /// <param name="sessionId">Сессия инициатора</param>
    /// <param name="request">Параметры создания</param>
    /// <returns>Созданная запись</returns>
    public Task<IdentityUser> CreateAsync(Guid actor, Guid sessionId, CreateIdentityUserRequest request)
    {
        return invitations.CreateAsync(actor, sessionId, request);
    }

    /// <summary>
    /// Сохраняет изменения пользователя
    /// </summary>
    /// <param name="actor">Инициатор</param>
    /// <param name="sessionId">Сессия инициатора</param>
    /// <param name="userId">Пользователь</param>
    /// <param name="request">Параметры изменения</param>
    /// <returns>Обновлённая запись</returns>
    public Task<IdentityUser> UpdateAsync(Guid actor, Guid sessionId, Guid userId, UpdateIdentityUserRequest request)
    {
        return repository.UpdateAsync(actor, sessionId, userId, request);
    }

    /// <summary>
    /// Выдаёт новый временный доступ
    /// </summary>
    /// <param name="actor">Инициатор</param>
    /// <param name="sessionId">Сессия инициатора</param>
    /// <param name="userId">Пользователь</param>
    /// <param name="activation">Параметры активации</param>
    /// <param name="version">Версия карточки</param>
    /// <returns>Карточка и состояние письма</returns>
    public Task<IdentityUser> ReissueAsync(Guid actor, Guid sessionId, Guid userId, ActivateIdentityUserRequest? activation, Guid version)
    {
        return invitations.ReissueAsync(actor, sessionId, userId, activation, version);
    }

    /// <summary>Отключает учётную запись и связанную запись сотрудника</summary>
    /// <param name="actor">Администратор</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="userId">Отключаемая учётная запись</param>
    /// <param name="version">Ожидаемая версия карточки</param>
    /// <returns>Деактивированная карточка</returns>
    public Task<IdentityUser> DeactivateAsync(Guid actor, Guid sessionId, Guid userId, Guid version)
    {
        return repository.DeactivateAsync(actor, sessionId, userId, version);
    }
}
