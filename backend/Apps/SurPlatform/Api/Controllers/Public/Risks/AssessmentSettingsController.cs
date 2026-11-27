using Api.Extensions;
using Application.Models;
using Application.UseCases.Risks.Interfaces;
using Core.Api.Controllers.ControllerTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Public.Risks;

/// <summary>Операции встроенной оценки и решений риска</summary>
/// <param name="useCases">Операции текущего клиента</param>
[Authorize]
[Route("risk-assessment-settings")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class AssessmentSettingsController(IRiskUseCaseManager useCases) : BasePublicController
{
    /// <summary>Возвращает текущую денежную шкалу</summary>
    /// <returns>Результат операции</returns>
    [HttpGet("impact-scale")]
    public Task<ImpactScale?> CurrentScaleAsync()
    {
        return useCases.GetImpactScaleAsync(User.GetRiskActor(), null, false);
    }

    /// <summary>Возвращает историческую шкалу</summary>
    /// <param name="id">Версия шкалы</param>
    /// <returns>Результат операции</returns>
    [HttpGet("impact-scale/{id:guid}")]
    public Task<ImpactScale?> HistoricalScaleAsync(Guid id)
    {
        return useCases.GetImpactScaleAsync(User.GetRiskActor(), id, false);
    }

    /// <summary>Возвращает встроенную матрицу</summary>
    /// <returns>Результат операции</returns>
    [HttpGet("matrix")]
    public AssessmentMatrix Matrix()
    {
        return useCases.GetMatrix();
    }
}
