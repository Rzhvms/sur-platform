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
using System.Text.Json;

namespace Api.Controllers.Provider.Risks;

/// <summary>HTTP-контракт BusinessHistoryController</summary>
/// <param name="useCases">Прикладные операции выбранного клиента</param>
[Route(RiskRoutes.History)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class BusinessHistoryController(RiskUseCaseManager useCases) : BaseProviderController
{
    /// <summary>Относительный маршрут операции ExportRoute</summary>
    private const string ExportRoute = "exports";

    /// <summary>Читает историю включая ожидающие поручения</summary>
    /// <param name="period">Включительный диапазон дат UTC</param>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Данные операции текущего клиента</returns>
    [HttpGet]
    public Task<JsonElement[]> ListAsync([FromQuery] BusinessPeriod period, [Range(0, int.MaxValue)] int offset = 0, [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        return useCases.HistoryAsync(User.GetRiskActor(), period, offset, limit);
    }

    /// <summary>Фиксирует административный снимок периода</summary>
    /// <param name="period">Включительный диапазон дат UTC</param>
    /// <returns>Данные операции текущего клиента</returns>
    [HttpPost(ExportRoute)]
    [ProducesResponseType<BusinessExportDal>(StatusCodes.Status201Created)]
    public async Task<ActionResult<BusinessExportDal>> RequestExportAsync(BusinessPeriod period)
    {
        return StatusCode(StatusCodes.Status201Created, await useCases.RequestExportAsync(User.GetRiskActor(), period));
    }
}
