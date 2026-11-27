using Api.Constants;
using Api.Extensions;
using Application.Models;
using Application.UseCases.Risks;
using Application.UseCases.Risks.Dto.Request;
using Core.Api.Controllers.ControllerTypes;
using Domain.DalModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Provider.Risks;

/// <summary>Операции встроенной оценки и решений риска</summary>
/// <param name="useCases">Операции текущего клиента</param>
[Authorize]
[Route(RiskRoutes.AssessmentSettings)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class ProviderAssessmentSettingsController(RiskUseCaseManager useCases) : BaseProviderController
{
    /// <summary>Маршрут операции возвращает текущую денежную шкалу</summary>
    private const string CurrentScaleAsyncRoute = RiskRoutes.Scale;

    /// <summary>Возвращает текущую денежную шкалу</summary>
    /// <returns>Результат операции</returns>
    [HttpGet(CurrentScaleAsyncRoute)]
    public Task<ImpactScale?> CurrentScaleAsync()
    {
        return useCases.GetImpactScaleAsync(User.GetRiskActor(), null, true);
    }

    /// <summary>Маршрут операции публикует денежную шкалу</summary>
    private const string SaveScaleAsyncRoute = RiskRoutes.Scale;

    /// <summary>Публикует денежную шкалу</summary>
    /// <param name="request">Поля команды</param>
    /// <returns>Результат операции</returns>
    [HttpPut(SaveScaleAsyncRoute)]
    public Task<ImpactScale> SaveScaleAsync(ImpactScaleRequest request)
    {
        return useCases.SaveImpactScaleAsync(User.GetRiskActor(), request);
    }

    /// <summary>Маршрут операции возвращает пороги принятия</summary>
    private const string AcceptanceAsyncRoute = RiskRoutes.AcceptanceSettings;

    /// <summary>Возвращает пороги принятия</summary>
    /// <returns>Результат операции</returns>
    [HttpGet(AcceptanceAsyncRoute)]
    public Task<RiskAcceptanceSettingsDal> AcceptanceAsync()
    {
        return useCases.GetAcceptanceSettingsAsync(User.GetRiskActor());
    }

    /// <summary>Маршрут операции сохраняет пороги принятия</summary>
    private const string SaveAcceptanceAsyncRoute = RiskRoutes.AcceptanceSettings;

    /// <summary>Сохраняет пороги принятия</summary>
    /// <param name="request">Поля команды</param>
    /// <returns>Результат операции</returns>
    [HttpPut(SaveAcceptanceAsyncRoute)]
    public Task<RiskAcceptanceSettingsDal> SaveAcceptanceAsync(AcceptanceSettingsRequest request)
    {
        return useCases.SaveAcceptanceSettingsAsync(User.GetRiskActor(), request);
    }
}
