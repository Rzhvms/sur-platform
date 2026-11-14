using System.ComponentModel.DataAnnotations;
using Api.Constants;
using Api.Extensions;
using Application.Constants;
using Application.Models;
using Application.UseCases.Risks;
using Application.UseCases.Risks.Dto.Request;
using Core.Api.Controllers.ControllerTypes;
using Domain.DalModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Public.Risks;

/// <summary>Операции встроенной оценки и решений риска</summary>
/// <param name="useCases">Операции текущего клиента</param>
[Authorize]
[Route(RiskRoutes.Risks)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class RiskAssessmentController(RiskUseCaseManager useCases) : BasePublicController
{
    /// <summary>Маршрут операции читает историю оценок</summary>
    private const string ListAsyncRoute = RiskRoutes.ById + "/assessments";

    /// <summary>Читает историю оценок</summary>
    /// <param name="id">Риск</param>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции</returns>
    [HttpGet(ListAsyncRoute)]
    public Task<RiskAssessmentDal[]> ListAsync(Guid id, [Range(0, int.MaxValue)] int offset = 0, [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        return useCases.ListAssessmentsAsync(User.GetRiskActor(), id, offset, limit);
    }

    /// <summary>Маршрут операции рассчитывает предварительную оценку</summary>
    private const string PreviewAsyncRoute = RiskRoutes.ById + "/assessments/preview";

    /// <summary>Рассчитывает предварительную оценку</summary>
    /// <param name="id">Риск</param>
    /// <param name="request">Поля команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost(PreviewAsyncRoute)]
    public Task<AssessmentPreview> PreviewAsync(Guid id, AssessmentRequest request)
    {
        return useCases.PreviewAssessmentAsync(User.GetRiskActor(), id, request);
    }

    /// <summary>Маршрут операции создаёт оценку и активирует риск</summary>
    private const string SaveAsyncRoute = RiskRoutes.ById + "/assessments";

    /// <summary>Создаёт оценку и активирует риск</summary>
    /// <param name="id">Риск</param>
    /// <param name="request">Поля команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost(SaveAsyncRoute)]
    [ProducesResponseType<AssessmentPreview>(StatusCodes.Status200OK)]
    public Task<AssessmentPreview> SaveAsync(Guid id, SaveAssessmentRequest request)
    {
        return useCases.SaveAssessmentAsync(User.GetRiskActor(), id, request);
    }

    /// <summary>Маршрут операции принимает риск</summary>
    private const string AcceptAsyncRoute = RiskRoutes.ById + "/accept";

    /// <summary>Принимает риск</summary>
    /// <param name="id">Риск</param>
    /// <param name="request">Поля команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost(AcceptAsyncRoute)]
    public Task<RiskDal> AcceptAsync(Guid id, RiskDecisionRequest request)
    {
        return useCases.ChooseDecisionAsync(User.GetRiskActor(), id, request, RiskRules.AcceptMode);
    }

    /// <summary>Маршрут операции выбирает обработку</summary>
    private const string TreatAsyncRoute = RiskRoutes.ById + "/treat";

    /// <summary>Выбирает обработку</summary>
    /// <param name="id">Риск</param>
    /// <param name="request">Поля команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost(TreatAsyncRoute)]
    public Task<RiskDal> TreatAsync(Guid id, RiskDecisionRequest request)
    {
        return useCases.ChooseDecisionAsync(User.GetRiskActor(), id, request, RiskRules.TreatMode);
    }

    /// <summary>Маршрут операции разделяет риск по процессам</summary>
    private const string SplitAsyncRoute = RiskRoutes.ById + "/split";

    /// <summary>Разделяет риск по процессам</summary>
    /// <param name="id">Риск</param>
    /// <param name="request">Поля команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost(SplitAsyncRoute)]
    public Task<RiskDal> SplitAsync(Guid id, RiskDecisionRequest request)
    {
        return useCases.ChooseDecisionAsync(User.GetRiskActor(), id, request, RiskRules.SplitMode);
    }

    /// <summary>Варианты процессов для разделения</summary>
    private const string SplitProcessesRoute = SplitAsyncRoute + "/processes";

    /// <summary>Возвращает процессы для выбора при разделении</summary>
    /// <param name="id">Риск</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Действующие процессы клиента</returns>
    [HttpGet(SplitProcessesRoute)]
    public Task<RiskProcessOption[]> SplitProcessesAsync(Guid id, [Range(0, int.MaxValue)] int offset = 0,
        [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        return useCases.ListSplitProcessesAsync(User.GetRiskActor(), id, offset, limit);
    }
}
