using System.ComponentModel.DataAnnotations;
using Api.Constants;
using Api.Extensions;
using Application.Constants;
using Application.Models;
using Application.UseCases.Treatments.Interfaces;
using Application.UseCases.Treatments.Dto.Request;
using Core.Api.Controllers.ControllerTypes;
using Domain.DalModels;
using Domain.DalModels.Risks;
using Domain.DalModels.Treatments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Public.Treatments;

/// <summary>HTTP-контракт операций с мерами</summary>
/// <param name="useCases">Операции текущего клиента</param>
[Authorize]
[Route("risks")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class RiskTreatmentController(ITreatmentUseCaseManager useCases) : BasePublicController
{
    /// <summary>Читает меры риска</summary>
    /// <param name="riskId">Риск</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции</returns>
    [HttpGet("{riskId:guid}/treatments")]
    [ProducesResponseType<TreatmentDal[]>(StatusCodes.Status200OK)]
    public Task<TreatmentDal[]> ListAsync(Guid riskId, [Range(0, int.MaxValue)] int offset = 0, [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        return useCases.ListTreatmentsAsync(User.GetRiskActor(), riskId, offset, limit);
    }

    /// <summary>Создаёт или сохраняет черновик</summary>
    /// <param name="riskId">Риск</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost("{riskId:guid}/treatments")]
    [ProducesResponseType<TreatmentDal>(StatusCodes.Status200OK)]
    public Task<TreatmentDal> CreateAsync(Guid riskId, TreatmentDraftRequest request)
    {
        return useCases.SaveTreatmentDraftAsync(User.GetRiskActor(), riskId, true, request);
    }

    /// <summary>Запускает весь ожидающий набор</summary>
    /// <param name="riskId">Риск</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost("{riskId:guid}/treatments/start")]
    [ProducesResponseType<RiskDal>(StatusCodes.Status200OK)]
    public Task<RiskDal> StartAsync(Guid riskId, TreatmentActionRequest request)
    {
        return useCases.StartTreatmentCycleAsync(User.GetRiskActor(), riskId, request);
    }
}
