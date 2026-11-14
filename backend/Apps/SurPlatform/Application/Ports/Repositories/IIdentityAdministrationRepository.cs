using Application.Models;
using Application.UseCases.Users.Dto.Request;

namespace Application.Ports.Repositories;

/// <summary>
/// Хранит административные настройки Identity в пределах текущей клиентской базы
/// </summary>
public interface IIdentityAdministrationRepository
{
    /// <summary>
    /// Возвращает страницу учётных записей
    /// </summary>
    /// <param name="after">Курсор предыдущей страницы</param>
    /// <param name="limit">Максимум строк</param>
    /// <returns>Страница учётных записей</returns>
    Task<IdentityUser[]> ListAsync(Guid after, int limit);

    /// <summary>
    /// Читает карточку интерактивного пользователя
    /// </summary>
    /// <param name="userId">Целевая учётная запись</param>
    /// <returns>Карточка или отсутствие записи</returns>
    Task<IdentityUser?> FindAsync(Guid userId);

    /// <summary>
    /// Читает системные роли и матрицу
    /// </summary>
    /// <returns>Действующая конфигурация ролей</returns>
    Task<IdentityRole[]> RolesAsync();

    /// <summary>
    /// Читает системный каталог прав
    /// </summary>
    /// <returns>Права каталога</returns>
    Task<IdentityRight[]> RightsAsync();

    /// <summary>
    /// Синхронизирует системный каталог с версией бэкенда
    /// </summary>
    /// <param name="actor">Инициатор с действующим правом администратора</param>
    /// <param name="sessionId">Сессия инициатора</param>
    Task SynchronizeCatalogAsync(Guid actor, Guid sessionId);

    /// <summary>
    /// Создаёт пользователя и первоначальную выдачу атомарно
    /// </summary>
    /// <param name="actor">Инициатор операции</param>
    /// <param name="sessionId">Сессия инициатора</param>
    /// <param name="request">Входные данные команды</param>
    /// <param name="passwordHash">Защищённое представление временного пароля</param>
    /// <returns>Данные для отправки письма</returns>
    Task<IdentityInvitation> CreateAsync(Guid actor, Guid sessionId, CreateIdentityUserRequest request, string passwordHash);

    /// <summary>
    /// Сохраняет разрешённые изменения пользователя
    /// </summary>
    /// <param name="actor">Инициатор операции</param>
    /// <param name="sessionId">Сессия инициатора</param>
    /// <param name="userId">Целевая учётная запись</param>
    /// <param name="request">Входные данные команды</param>
    /// <returns>Обновлённая карточка</returns>
    Task<IdentityUser> UpdateAsync(Guid actor, Guid sessionId, Guid userId, UpdateIdentityUserRequest request);

    /// <summary>
    /// Сохраняет матрицу роли и проверяет инварианты доступа
    /// </summary>
    /// <param name="actor">Инициатор операции</param>
    /// <param name="sessionId">Сессия инициатора</param>
    /// <param name="roleId">Изменяемая роль</param>
    /// <param name="request">Входные данные команды</param>
    /// <returns>Обновлённая роль</returns>
    Task<IdentityRole> SaveRoleAsync(Guid actor, Guid sessionId, Guid roleId, UpdateIdentityRoleRequest request);

    /// <summary>
    /// Повторно выдаёт доступ и при запросе активирует учётную запись
    /// </summary>
    /// <param name="actor">Инициатор операции</param>
    /// <param name="sessionId">Сессия инициатора</param>
    /// <param name="userId">Целевая учётная запись</param>
    /// <param name="activation">Параметры активации или null для повторной выдачи</param>
    /// <param name="version">Ожидаемая версия записи или выдачи по контексту команды</param>
    /// <param name="passwordHash">Защищённое представление временного пароля</param>
    /// <returns>Данные для отправки письма</returns>
    Task<IdentityInvitation> IssueAsync(Guid actor, Guid sessionId, Guid userId, ActivateIdentityUserRequest? activation, Guid version, string passwordHash);

    /// <summary>
    /// Создаёт первого администратора или повторно выдаёт ему доступ
    /// </summary>
    /// <param name="email">Настроенная почта первого администратора</param>
    /// <param name="fullName">Настроенное ФИО первого администратора</param>
    /// <param name="passwordHash">Защищённое представление временного пароля</param>
    /// <param name="reissue">Признак явной служебной повторной выдачи</param>
    /// <returns>Данные письма при новой выдаче</returns>
    Task<IdentityInvitation?> BootstrapAsync(string email, string fullName, string passwordHash, bool reissue);

    /// <summary>
    /// Проверяет актуальность поручения перед доставкой
    /// </summary>
    /// <param name="userId">Целевая учётная запись</param>
    /// <param name="version">Ожидаемая версия записи или выдачи по контексту команды</param>
    /// <returns>Признак актуальной выдачи</returns>
    Task<bool> IsInvitationCurrentAsync(Guid userId, Guid version);

    /// <summary>
    /// Сохраняет результат только для текущей выдачи
    /// </summary>
    /// <param name="userId">Целевая учётная запись</param>
    /// <param name="version">Ожидаемая версия записи или выдачи по контексту команды</param>
    /// <param name="status">Подтверждённый результат отправки</param>
    Task FinishInvitationAsync(Guid userId, Guid version, string status);

    /// <summary>Отключает учётную запись и связанную запись сотрудника</summary>
    /// <param name="actor">Администратор</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="userId">Отключаемая учётная запись</param>
    /// <param name="version">Ожидаемая версия карточки</param>
    /// <returns>Деактивированная карточка</returns>
    Task<IdentityUser> DeactivateAsync(Guid actor, Guid sessionId, Guid userId, Guid version);
}
