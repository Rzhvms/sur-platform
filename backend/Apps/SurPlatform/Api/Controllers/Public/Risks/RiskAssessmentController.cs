using System.ComponentModel.DataAnnotations;
using Api.Constants;
using Api.Extensions;
using Application.Constants;
using Application.Models;
using Application.UseCases.Risks.Interfaces;
using Application.UseCases.Risks.Dto.Request;
using Core.Api.Controllers.ControllerTypes;
using Domain.DalModels;
using Domain.DalModels.Assessments;
using Domain.DalModels.Risks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Public.Risks;

/// <summary>Операции встроенной оценки и решений риска</summary>
/// <param name="useCases">Операции текущего клиента</param>
[Authorize]
[Route("risks")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class RiskAssessmentController(IRiskUseCaseManager useCases) : BasePublicController
{
    /// <summary>Читает историю оценок</summary>
    /// <param name="id">Риск</param>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции</returns>
    [HttpGet("{id:guid}/assessments")]
    public Task<RiskAssessmentDal[]> ListAsync(Guid id, [Range(0, int.MaxValue)] int offset = 0, [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        return useCases.ListAssessmentsAsync(User.GetRiskActor(), id, offset, limit);
    }

    /// <summary>Рассчитывает предварительную оценку</summary>
    /// <param name="id">Риск</param>
    /// <param name="request">Поля команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost("{id:guid}/assessments/preview")]
    public Task<AssessmentPreview> PreviewAsync(Guid id, AssessmentRequest request)
    {
        return useCases.PreviewAssessmentAsync(User.GetRiskActor(), id, request);
    }

    /// <summary>Создаёт оценку и активирует риск</summary>
    /// <param name="id">Риск</param>
    /// <param name="request">Поля команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost("{id:guid}/assessments")]
    [ProducesResponseType<AssessmentPreview>(StatusCodes.Status200OK)]
    public Task<AssessmentPreview> SaveAsync(Guid id, SaveAssessmentRequest request)
    {
        return useCases.SaveAssessmentAsync(User.GetRiskActor(), id, request);
    }

    /// <summary>Принимает риск</summary>
    /// <param name="id">Риск</param>
    /// <param name="request">Поля команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost("{id:guid}/accept")]
    public Task<RiskDal> AcceptAsync(Guid id, RiskDecisionRequest request)
    {
        return useCases.ChooseDecisionAsync(User.GetRiskActor(), id, request, RiskRules.AcceptMode);
    }

    /// <summary>Выбирает обработку</summary>
    /// <param name="id">Риск</param>
    /// <param name="request">Поля команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost("{id:guid}/treat")]
    public Task<RiskDal> TreatAsync(Guid id, RiskDecisionRequest request)
    {
        return useCases.ChooseDecisionAsync(User.GetRiskActor(), id, request, RiskRules.TreatMode);
    }

    /// <summary>Разделяет риск по процессам</summary>
    /// <param name="id">Риск</param>
    /// <param name="request">Поля команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost("{id:guid}/split")]
    public Task<RiskDal> SplitAsync(Guid id, RiskDecisionRequest request)
    {
        return useCases.ChooseDecisionAsync(User.GetRiskActor(), id, request, RiskRules.SplitMode);
    }

    /// <summary>Возвращает процессы для выбора при разделении</summary>
    /// <param name="id">Риск</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Действующие процессы клиента</returns>
    [HttpGet("{id:guid}/split/processes")]
    public Task<RiskProcessOption[]> SplitProcessesAsync(Guid id, [Range(0, int.MaxValue)] int offset = 0,
        [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        return useCases.ListSplitProcessesAsync(User.GetRiskActor(), id, offset, limit);
    }
}
