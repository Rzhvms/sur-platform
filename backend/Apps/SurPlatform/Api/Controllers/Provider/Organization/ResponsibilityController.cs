using Api.Constants;
using Application.Models;
using Application.UseCases.Organization.Dto.Request;
using Domain.DalModels;
using Application.Constants;
using Application.UseCases.Organization;
using Core.Api.Controllers.ControllerTypes;
using Core.Api.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Provider.Organization;

/// <summary>
/// Административные операции справочника оргструктуры
/// </summary>
/// <param name="useCases">Сценарии оргструктуры клиента</param>
[Route(ResponsibilityController.RootRoute)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class ResponsibilityController(OrganizationUseCaseManager useCases) : BaseProviderController
{
    /// <summary>
    /// Маршрут справочника после префикса provider
    /// </summary>
    internal const string RootRoute = OrganizationRoutes.Root + "responsibilities";

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
    [HttpGet]
    public async Task<ActionResult<ResponsibilityDal[]>> ListResponsibilityAsync(Guid after = default, int limit = OrganizationRoutes.DefaultPageSize, bool? active = null, string? search = null, Guid? directorateId = null, string? responsibilityType = null)
    {
        if (limit is < 1 or > OrganizationRoutes.MaxPageSize || search?.Length > OrganizationRoutes.MaxSearchLength)
        {
            return BadRequest();
        }

        return await useCases.ListResponsibilityAsync(after, limit, active, search, directorateId, responsibilityType);
    }

    /// <summary>
    /// Читает карточку справочника
    /// </summary>
    /// <param name="id">Идентификатор записи</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    [HttpGet(OrganizationRoutes.ById)]
    public async Task<ActionResult<ResponsibilityDal>> GetResponsibilityAsync(Guid id)
    {
        return await useCases.GetResponsibilityAsync(id);
    }

    /// <summary>
    /// Создаёт запись справочника
    /// </summary>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    [HttpPost]
    [ProducesResponseType<ResponsibilityDal>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ResponsibilityDal>> CreateResponsibilityAsync(ResponsibilityRequest request)
    {
        return StatusCode(StatusCodes.Status201Created, await useCases.CreateResponsibilityAsync(User.GetUserId(), SessionId, request));
    }

    /// <summary>
    /// Проверяет зависимости и меняет состояние записи
    /// </summary>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    [HttpPut(OrganizationRoutes.State)]
    public async Task<ActionResult<ResponsibilityDal>> SetResponsibilityStateAsync(Guid id, OrgStateRequest request)
    {
        return await useCases.SetResponsibilityStateAsync(User.GetUserId(), SessionId, id, request);
    }

    /// <summary>
    /// Относительный маршрут операции
    /// </summary>
    private const string GetSlotRoute = "slot";

    /// <summary>
    /// Проверяет занятость позиции перед выбором
    /// </summary>
    /// <param name="responsibilityType">Код ответственности</param>
    /// <param name="directorateId">Область дирекции</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    [HttpGet(GetSlotRoute)]
    public async Task<ActionResult<OrganizationSlot>> GetSlotAsync(string responsibilityType, Guid? directorateId = null)
    {
        return await useCases.GetSlotAsync(responsibilityType, directorateId);
    }

    /// <summary>Идентификатор сессии из проверенного access-токена</summary>
    private Guid SessionId
    {
        get { return Guid.Parse(User.FindFirst(AccessTokenClaimTypes.SessionId)?.Value ?? string.Empty); }
    }
}
