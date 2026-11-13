using Application.Models;
using Application.UseCases.Organization.Dto.Request;
using Domain.DalModels;

namespace Application.Ports.Repositories;

/// <summary>
/// Справочники и административные команды оргструктуры
/// </summary>
public interface IOrganizationRepository
{
    /// <summary>
    /// Читает страницу справочника
    /// </summary>
    /// <param name="after">Курсор предыдущей страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <param name="active">Фильтр состояния</param>
    /// <param name="search">Фрагмент названия, ФИО, адреса или должности</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<DirectorateDal[]> ListDirectorateAsync(Guid after, int limit, bool? active, string? search);

    /// <summary>
    /// Читает карточку справочника
    /// </summary>
    /// <param name="id">Идентификатор записи</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<DirectorateDal> GetDirectorateAsync(Guid id);

    /// <summary>
    /// Создаёт запись справочника
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<DirectorateDal> CreateDirectorateAsync(Guid actor, Guid sessionId, DirectorateRequest request);

    /// <summary>
    /// Изменяет отображаемые поля записи
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <param name="version">Ожидаемая версия записи</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<DirectorateDal> UpdateDirectorateAsync(Guid actor, Guid sessionId, Guid id, DirectorateRequest request, Guid version);

    /// <summary>
    /// Проверяет зависимости и меняет состояние записи
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<DirectorateDal> SetDirectorateStateAsync(Guid actor, Guid sessionId, Guid id, OrgStateRequest request);

    /// <summary>
    /// Читает страницу справочника
    /// </summary>
    /// <param name="after">Курсор предыдущей страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <param name="active">Фильтр состояния</param>
    /// <param name="search">Фрагмент названия, ФИО, адреса или должности</param>
    /// <param name="directorateId">Область дирекции</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<OrgPersonDal[]> ListPersonAsync(Guid after, int limit, bool? active, string? search, Guid? directorateId);

    /// <summary>
    /// Читает карточку справочника
    /// </summary>
    /// <param name="id">Идентификатор записи</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<OrgPersonDal> GetPersonAsync(Guid id);

    /// <summary>
    /// Создаёт запись справочника
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<OrgPersonDal> CreatePersonAsync(Guid actor, Guid sessionId, PersonRequest request);

    /// <summary>
    /// Изменяет отображаемые поля записи
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<OrgPersonDal> UpdatePersonAsync(Guid actor, Guid sessionId, Guid id, PersonEditRequest request);

    /// <summary>
    /// Проверяет зависимости и меняет состояние записи
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<OrgPersonDal> SetPersonStateAsync(Guid actor, Guid sessionId, Guid id, OrgStateRequest request);

    /// <summary>
    /// Читает страницу справочника
    /// </summary>
    /// <param name="after">Курсор предыдущей страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <param name="active">Фильтр состояния</param>
    /// <param name="search">Фрагмент названия, ФИО, адреса или должности</param>
    /// <param name="directorateId">Область дирекции</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<ProcessDal[]> ListProcessAsync(Guid after, int limit, bool? active, string? search, Guid? directorateId);

    /// <summary>
    /// Читает карточку справочника
    /// </summary>
    /// <param name="id">Идентификатор записи</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<ProcessDal> GetProcessAsync(Guid id);

    /// <summary>
    /// Создаёт запись справочника
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<ProcessDal> CreateProcessAsync(Guid actor, Guid sessionId, ProcessRequest request);

    /// <summary>
    /// Изменяет отображаемые поля записи
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<ProcessDal> UpdateProcessAsync(Guid actor, Guid sessionId, Guid id, ProcessEditRequest request);

    /// <summary>
    /// Проверяет зависимости и меняет состояние записи
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<ProcessDal> SetProcessStateAsync(Guid actor, Guid sessionId, Guid id, OrgStateRequest request);

    /// <summary>
    /// Читает страницу справочника
    /// </summary>
    /// <param name="after">Курсор предыдущей страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <param name="active">Фильтр состояния</param>
    /// <param name="search">Фрагмент названия, ФИО, адреса или должности</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<AssetDal[]> ListAssetAsync(Guid after, int limit, bool? active, string? search);

    /// <summary>
    /// Читает карточку справочника
    /// </summary>
    /// <param name="id">Идентификатор записи</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<AssetDal> GetAssetAsync(Guid id);

    /// <summary>
    /// Создаёт запись справочника
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<AssetDal> CreateAssetAsync(Guid actor, Guid sessionId, AssetRequest request);

    /// <summary>
    /// Изменяет отображаемые поля записи
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <param name="version">Ожидаемая версия записи</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<AssetDal> UpdateAssetAsync(Guid actor, Guid sessionId, Guid id, AssetRequest request, Guid version);

    /// <summary>
    /// Проверяет зависимости и меняет состояние записи
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<AssetDal> SetAssetStateAsync(Guid actor, Guid sessionId, Guid id, OrgStateRequest request);

    /// <summary>
    /// Читает страницу справочника
    /// </summary>
    /// <param name="after">Курсор предыдущей страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <param name="active">Фильтр состояния</param>
    /// <param name="search">Фрагмент названия, ФИО, адреса или должности</param>
    /// <param name="directorateId">Область дирекции</param>
    /// <param name="responsibilityType">Фильтр по точному коду ответственности</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<ResponsibilityDal[]> ListResponsibilityAsync(Guid after, int limit, bool? active, string? search, Guid? directorateId, string? responsibilityType);

    /// <summary>
    /// Читает карточку справочника
    /// </summary>
    /// <param name="id">Идентификатор записи</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<ResponsibilityDal> GetResponsibilityAsync(Guid id);

    /// <summary>
    /// Создаёт запись справочника
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<ResponsibilityDal> CreateResponsibilityAsync(Guid actor, Guid sessionId, ResponsibilityRequest request);

    /// <summary>
    /// Проверяет зависимости и меняет состояние записи
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<ResponsibilityDal> SetResponsibilityStateAsync(Guid actor, Guid sessionId, Guid id, OrgStateRequest request);

    /// <summary>
    /// Подтверждает замену владельца процесса
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<ProcessDal> ChangeOwnerAsync(Guid actor, Guid sessionId, Guid id, OwnerRequest request);

    /// <summary>
    /// Проверяет занятость позиции перед выбором
    /// </summary>
    /// <param name="responsibilityType">Код ответственности</param>
    /// <param name="directorateId">Область дирекции</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    Task<OrganizationSlot> GetSlotAsync(string responsibilityType, Guid? directorateId);

    /// <summary>Читает организационные зависимости для подтверждения деактивации</summary>
    /// <param name="userId">Учётная запись</param>
    /// <returns>Действующий сотрудник, владение и назначения</returns>
    Task<OrganizationUserContext> GetUserContextAsync(Guid userId);
}
