using System.ComponentModel.DataAnnotations;
using Api.Constants;
using Api.Extensions;
using Application.Constants;
using Application.Models;
using Application.UseCases.Risks.Interfaces;
using Application.UseCases.Risks.Dto;
using Core.Api.Controllers.ControllerTypes;
using Domain.DalModels;
using Domain.DalModels.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Public.Risks;

/// <summary>HTTP-контракт RiskReferenceController</summary>
/// <param name="useCases">Прикладные операции выбранного клиента</param>
[Authorize]
[Route("risk-references")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class RiskReferenceController(IRiskUseCaseManager useCases) : BasePublicController
{
    /// <summary>Возвращает страницу справочника</summary>
    /// <param name="kind">Тип справочника</param>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <param name="search">Фрагмент названия</param>
    /// <returns>Данные операции текущего клиента</returns>
    [HttpGet("{kind}")]
    public Task<RiskReference[]> ListAsync(RiskReferenceKind kind, [Range(0, int.MaxValue)] int offset = 0, [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage, [StringLength(250)] string? search = null)
    {
        return useCases.ListReferencesAsync(User.GetRiskActor(), kind, available: true, offset, limit, search);
    }

    /// <summary>Возвращает разрешённые варианты для паспорта риска</summary>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Варианты текущего клиента</returns>
    [HttpGet("processes")]
    public Task<RiskProcessOption[]> ListProcessesAsync([Range(0, int.MaxValue)] int offset = 0,
        [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        return useCases.ListProcessesAsync(User.GetRiskActor(), offset, limit);
    }

    /// <summary>Возвращает разрешённые варианты для паспорта риска</summary>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Варианты текущего клиента</returns>
    [HttpGet("assets")]
    public Task<AssetDal[]> ListAssetsAsync([Range(0, int.MaxValue)] int offset = 0,
        [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        return useCases.ListAssetsAsync(User.GetRiskActor(), offset, limit);
    }
}
