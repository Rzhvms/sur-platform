using Application.Models;
using Application.UseCases.Organization.Dto.Request;
using Domain.DalModels;
using Application.Ports.Repositories;

namespace Application.UseCases.Organization;

/// <summary>
/// Координирует административные операции оргструктуры
/// </summary>
/// <param name="repository">Хранилище выбранного клиента</param>
public sealed class OrganizationUseCaseManager(IOrganizationRepository repository)
{
    /// <summary>
    /// Читает страницу справочника
    /// </summary>
    /// <param name="after">Курсор предыдущей страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <param name="active">Фильтр состояния</param>
    /// <param name="search">Фрагмент названия, ФИО, адреса или должности</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<DirectorateDal[]> ListDirectorateAsync(Guid after, int limit, bool? active, string? search)
    {
        return repository.ListDirectorateAsync(after, limit, active, search);
    }

    /// <summary>
    /// Читает карточку справочника
    /// </summary>
    /// <param name="id">Идентификатор записи</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<DirectorateDal> GetDirectorateAsync(Guid id)
    {
        return repository.GetDirectorateAsync(id);
    }

    /// <summary>
    /// Создаёт запись справочника
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<DirectorateDal> CreateDirectorateAsync(Guid actor, Guid sessionId, DirectorateRequest request)
    {
        return repository.CreateDirectorateAsync(actor, sessionId, request);
    }

    /// <summary>
    /// Изменяет отображаемые поля записи
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <param name="version">Ожидаемая версия записи</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<DirectorateDal> UpdateDirectorateAsync(Guid actor, Guid sessionId, Guid id, DirectorateRequest request, Guid version)
    {
        return repository.UpdateDirectorateAsync(actor, sessionId, id, request, version);
    }

    /// <summary>
    /// Проверяет зависимости и меняет состояние записи
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<DirectorateDal> SetDirectorateStateAsync(Guid actor, Guid sessionId, Guid id, OrgStateRequest request)
    {
        return repository.SetDirectorateStateAsync(actor, sessionId, id, request);
    }

    /// <summary>
    /// Читает страницу справочника
    /// </summary>
    /// <param name="after">Курсор предыдущей страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <param name="active">Фильтр состояния</param>
    /// <param name="search">Фрагмент названия, ФИО, адреса или должности</param>
    /// <param name="directorateId">Область дирекции</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<OrgPersonDal[]> ListPersonAsync(Guid after, int limit, bool? active, string? search, Guid? directorateId)
    {
        return repository.ListPersonAsync(after, limit, active, search, directorateId);
    }

    /// <summary>
    /// Читает карточку справочника
    /// </summary>
    /// <param name="id">Идентификатор записи</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<OrgPersonDal> GetPersonAsync(Guid id)
    {
        return repository.GetPersonAsync(id);
    }

    /// <summary>
    /// Создаёт запись справочника
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<OrgPersonDal> CreatePersonAsync(Guid actor, Guid sessionId, PersonRequest request)
    {
        return repository.CreatePersonAsync(actor, sessionId, request);
    }

    /// <summary>
    /// Изменяет отображаемые поля записи
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<OrgPersonDal> UpdatePersonAsync(Guid actor, Guid sessionId, Guid id, PersonEditRequest request)
    {
        return repository.UpdatePersonAsync(actor, sessionId, id, request);
    }

    /// <summary>
    /// Проверяет зависимости и меняет состояние записи
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<OrgPersonDal> SetPersonStateAsync(Guid actor, Guid sessionId, Guid id, OrgStateRequest request)
    {
        return repository.SetPersonStateAsync(actor, sessionId, id, request);
    }

    /// <summary>
    /// Читает страницу справочника
    /// </summary>
    /// <param name="after">Курсор предыдущей страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <param name="active">Фильтр состояния</param>
    /// <param name="search">Фрагмент названия, ФИО, адреса или должности</param>
    /// <param name="directorateId">Область дирекции</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<ProcessDal[]> ListProcessAsync(Guid after, int limit, bool? active, string? search, Guid? directorateId)
    {
        return repository.ListProcessAsync(after, limit, active, search, directorateId);
    }

    /// <summary>
    /// Читает карточку справочника
    /// </summary>
    /// <param name="id">Идентификатор записи</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<ProcessDal> GetProcessAsync(Guid id)
    {
        return repository.GetProcessAsync(id);
    }

    /// <summary>
    /// Создаёт запись справочника
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<ProcessDal> CreateProcessAsync(Guid actor, Guid sessionId, ProcessRequest request)
    {
        return repository.CreateProcessAsync(actor, sessionId, request);
    }

    /// <summary>
    /// Изменяет отображаемые поля записи
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<ProcessDal> UpdateProcessAsync(Guid actor, Guid sessionId, Guid id, ProcessEditRequest request)
    {
        return repository.UpdateProcessAsync(actor, sessionId, id, request);
    }

    /// <summary>
    /// Проверяет зависимости и меняет состояние записи
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<ProcessDal> SetProcessStateAsync(Guid actor, Guid sessionId, Guid id, OrgStateRequest request)
    {
        return repository.SetProcessStateAsync(actor, sessionId, id, request);
    }

    /// <summary>
    /// Читает страницу справочника
    /// </summary>
    /// <param name="after">Курсор предыдущей страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <param name="active">Фильтр состояния</param>
    /// <param name="search">Фрагмент названия, ФИО, адреса или должности</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<AssetDal[]> ListAssetAsync(Guid after, int limit, bool? active, string? search)
    {
        return repository.ListAssetAsync(after, limit, active, search);
    }

    /// <summary>
    /// Читает карточку справочника
    /// </summary>
    /// <param name="id">Идентификатор записи</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<AssetDal> GetAssetAsync(Guid id)
    {
        return repository.GetAssetAsync(id);
    }

    /// <summary>
    /// Создаёт запись справочника
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<AssetDal> CreateAssetAsync(Guid actor, Guid sessionId, AssetRequest request)
    {
        return repository.CreateAssetAsync(actor, sessionId, request);
    }

    /// <summary>
    /// Изменяет отображаемые поля записи
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <param name="version">Ожидаемая версия записи</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<AssetDal> UpdateAssetAsync(Guid actor, Guid sessionId, Guid id, AssetRequest request, Guid version)
    {
        return repository.UpdateAssetAsync(actor, sessionId, id, request, version);
    }

    /// <summary>
    /// Проверяет зависимости и меняет состояние записи
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<AssetDal> SetAssetStateAsync(Guid actor, Guid sessionId, Guid id, OrgStateRequest request)
    {
        return repository.SetAssetStateAsync(actor, sessionId, id, request);
    }

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
    public Task<ResponsibilityDal[]> ListResponsibilityAsync(Guid after, int limit, bool? active, string? search, Guid? directorateId, string? responsibilityType)
    {
        return repository.ListResponsibilityAsync(after, limit, active, search, directorateId, responsibilityType);
    }

    /// <summary>
    /// Читает карточку справочника
    /// </summary>
    /// <param name="id">Идентификатор записи</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<ResponsibilityDal> GetResponsibilityAsync(Guid id)
    {
        return repository.GetResponsibilityAsync(id);
    }

    /// <summary>
    /// Создаёт запись справочника
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<ResponsibilityDal> CreateResponsibilityAsync(Guid actor, Guid sessionId, ResponsibilityRequest request)
    {
        return repository.CreateResponsibilityAsync(actor, sessionId, request);
    }

    /// <summary>
    /// Проверяет зависимости и меняет состояние записи
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<ResponsibilityDal> SetResponsibilityStateAsync(Guid actor, Guid sessionId, Guid id, OrgStateRequest request)
    {
        return repository.SetResponsibilityStateAsync(actor, sessionId, id, request);
    }

    /// <summary>
    /// Подтверждает замену владельца процесса
    /// </summary>
    /// <param name="actor">Идентификатор администратора</param>
    /// <param name="sessionId">Действующая сессия администратора</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<ProcessDal> ChangeOwnerAsync(Guid actor, Guid sessionId, Guid id, OwnerRequest request)
    {
        return repository.ChangeOwnerAsync(actor, sessionId, id, request);
    }

    /// <summary>
    /// Проверяет занятость позиции перед выбором
    /// </summary>
    /// <param name="responsibilityType">Код ответственности</param>
    /// <param name="directorateId">Область дирекции</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    public Task<OrganizationSlot> GetSlotAsync(string responsibilityType, Guid? directorateId)
    {
        return repository.GetSlotAsync(responsibilityType, directorateId);
    }

    /// <summary>Читает организационные зависимости для подтверждения деактивации</summary>
    /// <param name="userId">Учётная запись</param>
    /// <returns>Действующий сотрудник, владение и назначения</returns>
    public Task<OrganizationUserContext> GetUserContextAsync(Guid userId)
    {
        return repository.GetUserContextAsync(userId);
    }
}
