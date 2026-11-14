using System.ComponentModel.DataAnnotations;
using Api.Constants;
using Api.Extensions;
using Application.UseCases.Risks;
using Core.Api.Controllers.ControllerTypes;
using Domain.DalModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Provider.Risks;

/// <summary>Административная история запусков, включая пустые срабатывания</summary>
/// <param name="useCases">Операции выбранной клиентской базы</param>
[Authorize]
[Route(RunsRoute)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class ReviewRunController(ReviewUseCaseManager useCases) : BaseProviderController
{
    /// <summary>Относительный маршрут истории запусков</summary>
    private const string RunsRoute = "review-runs";

    /// <summary>Возвращает результаты первичной обработки</summary>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>История запусков</returns>
    [HttpGet]
    [ProducesResponseType<ReviewRunDal[]>(StatusCodes.Status200OK)]
    public Task<ReviewRunDal[]> ListAsync([Range(0, int.MaxValue)] int offset = 0,
        [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        return useCases.ListReviewRunsAsync(User.GetRiskActor(), offset, limit);
    }
}
