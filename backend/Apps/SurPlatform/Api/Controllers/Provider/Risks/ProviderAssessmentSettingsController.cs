using Api.Extensions;
using Application.Models;
using Application.UseCases.Risks.Interfaces;
using Application.UseCases.Risks.Dto.Request;
using Core.Api.Controllers.ControllerTypes;
using Domain.DalModels;
using Domain.DalModels.Assessments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Provider.Risks;

/// <summary>Операции встроенной оценки и решений риска</summary>
/// <param name="useCases">Операции текущего клиента</param>
[Authorize]
[Route("risk-assessment-settings")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class ProviderAssessmentSettingsController(IRiskUseCaseManager useCases) : BaseProviderController
{
    /// <summary>Возвращает текущую денежную шкалу</summary>
    /// <returns>Результат операции</returns>
    [HttpGet("impact-scale")]
    public Task<ImpactScale?> CurrentScaleAsync()
    {
        return useCases.GetImpactScaleAsync(User.GetRiskActor(), null, true);
    }

    /// <summary>Публикует денежную шкалу</summary>
    /// <param name="request">Поля команды</param>
    /// <returns>Результат операции</returns>
    [HttpPut("impact-scale")]
    public Task<ImpactScale> SaveScaleAsync(ImpactScaleRequest request)
    {
        return useCases.SaveImpactScaleAsync(User.GetRiskActor(), request);
    }

    /// <summary>Возвращает пороги принятия</summary>
    /// <returns>Результат операции</returns>
    [HttpGet("acceptance")]
    public Task<RiskAcceptanceSettingsDal> AcceptanceAsync()
    {
        return useCases.GetAcceptanceSettingsAsync(User.GetRiskActor());
    }

    /// <summary>Сохраняет пороги принятия</summary>
    /// <param name="request">Поля команды</param>
    /// <returns>Результат операции</returns>
    [HttpPut("acceptance")]
    public Task<RiskAcceptanceSettingsDal> SaveAcceptanceAsync(AcceptanceSettingsRequest request)
    {
        return useCases.SaveAcceptanceSettingsAsync(User.GetRiskActor(), request);
    }
}
