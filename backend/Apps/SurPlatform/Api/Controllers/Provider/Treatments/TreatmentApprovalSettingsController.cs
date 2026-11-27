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

namespace Api.Controllers.Provider.Treatments;

/// <summary>HTTP-контракт операций с мерами</summary>
/// <param name="useCases">Операции текущего клиента</param>
[Authorize]
[Route(TreatmentRoute)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class TreatmentApprovalSettingsController(TreatmentUseCaseManager useCases) : BaseProviderController
{
    /// <summary>Базовый маршрут контроллера</summary>
    private const string TreatmentRoute = "treatment-approval-settings";

    /// <summary>Читает пороги согласования</summary>
    /// <returns>Результат операции</returns>
    [HttpGet]
    [ProducesResponseType<TreatmentApprovalSettingsDal>(StatusCodes.Status200OK)]
    public Task<TreatmentApprovalSettingsDal> GetAsync()
    {
        return useCases.GetTreatmentSettingsAsync(User.GetRiskActor());
    }

    /// <summary>Сохраняет пороги согласования</summary>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPut]
    [ProducesResponseType<TreatmentApprovalSettingsDal>(StatusCodes.Status200OK)]
    public Task<TreatmentApprovalSettingsDal> SaveAsync(TreatmentThresholdsRequest request)
    {
        return useCases.SaveTreatmentSettingsAsync(User.GetRiskActor(), request);
    }
}
