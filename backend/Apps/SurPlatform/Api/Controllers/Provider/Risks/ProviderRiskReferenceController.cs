using System.ComponentModel.DataAnnotations;
using Api.Constants;
using Api.Extensions;
using Application.Constants;
using Application.Models;
using Application.UseCases.Risks;
using Application.UseCases.Risks.Dto;
using Core.Api.Controllers.ControllerTypes;
using Domain.DalModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Provider.Risks;

/// <summary>HTTP-контракт ProviderRiskReferenceController</summary>
/// <param name="useCases">Прикладные операции выбранного клиента</param>
[Route(RiskRoutes.References)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class ProviderRiskReferenceController(RiskUseCaseManager useCases) : BaseProviderController
{
    /// <summary>Относительный маршрут операции EntryRoute</summary>
    private const string EntryRoute = RiskRoutes.ByKind + "/" + RiskRoutes.ById;

    /// <summary>Возвращает страницу справочника</summary>
    /// <param name="kind">Тип справочника</param>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <param name="search">Фрагмент названия</param>
    /// <returns>Данные операции текущего клиента</returns>
    [HttpGet(RiskRoutes.ByKind)]
    public Task<RiskReference[]> ListAsync(RiskReferenceKind kind, [Range(0, int.MaxValue)] int offset = 0, [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage, [StringLength(250)] string? search = null)
    {
        return useCases.ListReferencesAsync(User.GetRiskActor(), kind, available: false, offset, limit, search);
    }

    /// <summary>Создаёт активную запись</summary>
    /// <param name="kind">Тип справочника</param>
    /// <param name="request">Поля команды</param>
    /// <returns>Данные операции текущего клиента</returns>
    [HttpPost(RiskRoutes.ByKind)]
    [ProducesResponseType<RiskReference>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RiskReference>> CreateAsync(RiskReferenceKind kind, RiskReferenceRequest request)
    {
        return StatusCode(StatusCodes.Status201Created, await useCases.SaveReferenceAsync(User.GetRiskActor(), kind, null, request));
    }

    /// <summary>Обновляет данные и активность записи</summary>
    /// <param name="kind">Тип справочника</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Поля команды</param>
    /// <returns>Данные операции текущего клиента</returns>
    [HttpPut(EntryRoute)]
    public Task<RiskReference> SaveAsync(RiskReferenceKind kind, Guid id, RiskReferenceRequest request)
    {
        return useCases.SaveReferenceAsync(User.GetRiskActor(), kind, id, request);
    }
}
