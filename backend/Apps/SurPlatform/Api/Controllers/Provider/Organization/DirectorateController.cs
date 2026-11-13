using Api.Constants;
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
[Route(DirectorateController.RootRoute)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class DirectorateController(OrganizationUseCaseManager useCases) : BaseProviderController
{
    /// <summary>
    /// Маршрут справочника после префикса provider
    /// </summary>
    internal const string RootRoute = OrganizationRoutes.Root + "directorates";

    /// <summary>
    /// Читает страницу справочника
    /// </summary>
    /// <param name="after">Курсор предыдущей страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <param name="active">Фильтр состояния</param>
    /// <param name="search">Фрагмент названия, ФИО, адреса или должности</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    [HttpGet]
    public async Task<ActionResult<DirectorateDal[]>> ListDirectorateAsync(Guid after = default, int limit = OrganizationRoutes.DefaultPageSize, bool? active = null, string? search = null)
    {
        if (limit is < 1 or > OrganizationRoutes.MaxPageSize || search?.Length > OrganizationRoutes.MaxSearchLength)
        {
            return BadRequest();
        }

        return await useCases.ListDirectorateAsync(after, limit, active, search);
    }

    /// <summary>
    /// Читает карточку справочника
    /// </summary>
    /// <param name="id">Идентификатор записи</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    [HttpGet(OrganizationRoutes.ById)]
    public async Task<ActionResult<DirectorateDal>> GetDirectorateAsync(Guid id)
    {
        return await useCases.GetDirectorateAsync(id);
    }

    /// <summary>
    /// Создаёт запись справочника
    /// </summary>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    [HttpPost]
    [ProducesResponseType<DirectorateDal>(StatusCodes.Status201Created)]
    public async Task<ActionResult<DirectorateDal>> CreateDirectorateAsync(DirectorateRequest request)
    {
        return StatusCode(StatusCodes.Status201Created, await useCases.CreateDirectorateAsync(User.GetUserId(), SessionId, request));
    }

    /// <summary>
    /// Изменяет отображаемые поля записи
    /// </summary>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <param name="version">Ожидаемая версия записи</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    [HttpPut(OrganizationRoutes.ById)]
    public async Task<ActionResult<DirectorateDal>> UpdateDirectorateAsync(Guid id, DirectorateRequest request, [FromQuery] Guid version)
    {
        return await useCases.UpdateDirectorateAsync(User.GetUserId(), SessionId, id, request, version);
    }

    /// <summary>
    /// Проверяет зависимости и меняет состояние записи
    /// </summary>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    [HttpPut(OrganizationRoutes.State)]
    public async Task<ActionResult<DirectorateDal>> SetDirectorateStateAsync(Guid id, OrgStateRequest request)
    {
        return await useCases.SetDirectorateStateAsync(User.GetUserId(), SessionId, id, request);
    }

    /// <summary>Идентификатор сессии из проверенного access-токена</summary>
    private Guid SessionId
    {
        get { return Guid.Parse(User.FindFirst(AccessTokenClaimTypes.SessionId)?.Value ?? string.Empty); }
    }
}
