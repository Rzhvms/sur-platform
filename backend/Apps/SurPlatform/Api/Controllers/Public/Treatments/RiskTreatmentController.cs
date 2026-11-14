using System.ComponentModel.DataAnnotations;
using Api.Constants;
using Api.Extensions;
using Application.Constants;
using Application.Models;
using Application.UseCases.Treatments;
using Application.UseCases.Treatments.Dto.Request;
using Core.Api.Controllers.ControllerTypes;
using Domain.DalModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Public.Treatments;

/// <summary>HTTP-контракт операций с мерами</summary>
/// <param name="useCases">Операции текущего клиента</param>
[Authorize]
[Route(RiskRoutes.Risks)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class RiskTreatmentController(TreatmentUseCaseManager useCases) : BasePublicController
{
    /// <summary>Маршрут операции List</summary>
    private const string ListRoute = "{riskId:guid}/treatments";

    /// <summary>Читает меры риска</summary>
    /// <param name="riskId">Риск</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции</returns>
    [HttpGet(ListRoute)]
    [ProducesResponseType<TreatmentDal[]>(StatusCodes.Status200OK)]
    public Task<TreatmentDal[]> ListAsync(Guid riskId, [Range(0, int.MaxValue)] int offset = 0, [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        return useCases.ListTreatmentsAsync(User.GetRiskActor(), riskId, offset, limit);
    }

    /// <summary>Маршрут операции Create</summary>
    private const string CreateRoute = "{riskId:guid}/treatments";

    /// <summary>Создаёт или сохраняет черновик</summary>
    /// <param name="riskId">Риск</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost(CreateRoute)]
    [ProducesResponseType<TreatmentDal>(StatusCodes.Status200OK)]
    public Task<TreatmentDal> CreateAsync(Guid riskId, TreatmentDraftRequest request)
    {
        return useCases.SaveTreatmentDraftAsync(User.GetRiskActor(), riskId, true, request);
    }

    /// <summary>Маршрут операции Start</summary>
    private const string StartRoute = "{riskId:guid}/treatments/start";

    /// <summary>Запускает весь ожидающий набор</summary>
    /// <param name="riskId">Риск</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost(StartRoute)]
    [ProducesResponseType<RiskDal>(StatusCodes.Status200OK)]
    public Task<RiskDal> StartAsync(Guid riskId, TreatmentActionRequest request)
    {
        return useCases.StartTreatmentCycleAsync(User.GetRiskActor(), riskId, request);
    }
}
