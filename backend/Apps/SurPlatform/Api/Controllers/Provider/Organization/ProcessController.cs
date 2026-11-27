using Application.UseCases.Organization.Dto.Request;
using Domain.DalModels;
using Application.Constants;
using Application.UseCases.Organization.Interfaces;
using Core.Api.Controllers.ControllerTypes;
using Core.Api.Extensions;
using Domain.DalModels.Organization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Provider.Organization;

/// <summary>
/// Административные операции справочника оргструктуры
/// </summary>
/// <param name="useCases">Сценарии оргструктуры клиента</param>
[Route("organization/processes")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class ProcessController(IOrganizationUseCaseManager useCases) : BaseProviderController
{
    /// <summary>
    /// Читает страницу справочника
    /// </summary>
    /// <param name="after">Курсор предыдущей страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <param name="active">Фильтр состояния</param>
    /// <param name="search">Фрагмент названия, ФИО, адреса или должности</param>
    /// <param name="directorateId">Область дирекции</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    [HttpGet]
    public async Task<ActionResult<ProcessDal[]>> ListProcessAsync(Guid after = default, int limit = OrganizationQueryLimits.DefaultPageSize, bool? active = null, string? search = null, Guid? directorateId = null)
    {
        return await useCases.ListProcessAsync(after, limit, active, search, directorateId);
    }

    /// <summary>
    /// Читает карточку справочника
    /// </summary>
    /// <param name="id">Идентификатор записи</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProcessDal>> GetProcessAsync(Guid id)
    {
        return await useCases.GetProcessAsync(id);
    }

    /// <summary>
    /// Создаёт запись справочника
    /// </summary>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    [HttpPost]
    [ProducesResponseType<ProcessDal>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ProcessDal>> CreateProcessAsync(ProcessRequest request)
    {
        return StatusCode(StatusCodes.Status201Created, await useCases.CreateProcessAsync(User.GetUserId(), SessionId, request));
    }

    /// <summary>
    /// Изменяет отображаемые поля записи
    /// </summary>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProcessDal>> UpdateProcessAsync(Guid id, ProcessEditRequest request)
    {
        return await useCases.UpdateProcessAsync(User.GetUserId(), SessionId, id, request);
    }

    /// <summary>
    /// Проверяет зависимости и меняет состояние записи
    /// </summary>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    [HttpPut("{id:guid}/state")]
    public async Task<ActionResult<ProcessDal>> SetProcessStateAsync(Guid id, OrgStateRequest request)
    {
        return await useCases.SetProcessStateAsync(User.GetUserId(), SessionId, id, request);
    }

    /// <summary>
    /// Подтверждает замену владельца процесса
    /// </summary>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции в текущей клиентской базе</returns>
    [HttpPut("{id:guid}/owner")]
    public async Task<ActionResult<ProcessDal>> ChangeOwnerAsync(Guid id, OwnerRequest request)
    {
        return await useCases.ChangeOwnerAsync(User.GetUserId(), SessionId, id, request);
    }

    /// <summary>Идентификатор сессии из проверенного access-токена</summary>
    private Guid SessionId => Guid.Parse(User.FindFirst(AccessTokenClaimTypes.SessionId)?.Value ?? string.Empty);
}
