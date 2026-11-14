using Api.Constants;
using Api.Extensions;
using Application.Models;
using Application.UseCases.Risks;
using Core.Api.Controllers.ControllerTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Public.Risks;

/// <summary>Операции встроенной оценки и решений риска</summary>
/// <param name="useCases">Операции текущего клиента</param>
[Authorize]
[Route(RiskRoutes.AssessmentSettings)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class AssessmentSettingsController(RiskUseCaseManager useCases) : BasePublicController
{
    /// <summary>Маршрут операции возвращает текущую денежную шкалу</summary>
    private const string CurrentScaleAsyncRoute = RiskRoutes.Scale;

    /// <summary>Возвращает текущую денежную шкалу</summary>
    /// <returns>Результат операции</returns>
    [HttpGet(CurrentScaleAsyncRoute)]
    public Task<ImpactScale?> CurrentScaleAsync()
    {
        return useCases.GetImpactScaleAsync(User.GetRiskActor(), null, false);
    }

    /// <summary>Маршрут операции возвращает историческую шкалу</summary>
    private const string HistoricalScaleAsyncRoute = RiskRoutes.Scale + "/" + RiskRoutes.ById;

    /// <summary>Возвращает историческую шкалу</summary>
    /// <param name="id">Версия шкалы</param>
    /// <returns>Результат операции</returns>
    [HttpGet(HistoricalScaleAsyncRoute)]
    public Task<ImpactScale?> HistoricalScaleAsync(Guid id)
    {
        return useCases.GetImpactScaleAsync(User.GetRiskActor(), id, false);
    }

    /// <summary>Маршрут операции возвращает встроенную матрицу</summary>
    private const string MatrixRoute = RiskRoutes.Matrix;

    /// <summary>Возвращает встроенную матрицу</summary>
    /// <returns>Результат операции</returns>
    [HttpGet(MatrixRoute)]
    public AssessmentMatrix Matrix()
    {
        return useCases.GetMatrix();
    }
}
