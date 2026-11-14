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
[Route(TreatmentRoute)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class TreatmentApprovalController(TreatmentUseCaseManager useCases) : BasePublicController
{
    /// <summary>Базовый маршрут контроллера</summary>
    private const string TreatmentRoute = "treatment-approvals";

    /// <summary>Читает очередь и её счётчик</summary>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции</returns>
    [HttpGet]
    [ProducesResponseType<TreatmentApprovalQueue>(StatusCodes.Status200OK)]
    public Task<TreatmentApprovalQueue> ListAsync([Range(0, int.MaxValue)] int offset = 0, [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        return useCases.ListTreatmentApprovalsAsync(User.GetRiskActor(), offset, limit);
    }

    /// <summary>Маршрут операции Approve</summary>
    private const string ApproveRoute = "{id:guid}/approve";

    /// <summary>Принимает решение по запросу</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost(ApproveRoute)]
    [ProducesResponseType<TreatmentApprovalDal>(StatusCodes.Status200OK)]
    public Task<TreatmentApprovalDal> ApproveAsync(Guid id, TreatmentActionRequest request)
    {
        return useCases.DecideTreatmentApprovalAsync(User.GetRiskActor(), id, TreatmentRules.Approved, request);
    }

    /// <summary>Маршрут операции Reject</summary>
    private const string RejectRoute = "{id:guid}/reject";

    /// <summary>Принимает решение по запросу</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost(RejectRoute)]
    [ProducesResponseType<TreatmentApprovalDal>(StatusCodes.Status200OK)]
    public Task<TreatmentApprovalDal> RejectAsync(Guid id, TreatmentActionRequest request)
    {
        return useCases.DecideTreatmentApprovalAsync(User.GetRiskActor(), id, TreatmentRules.Rejected, request);
    }

    /// <summary>Маршрут операции Revoke</summary>
    private const string RevokeRoute = "{id:guid}/revoke";

    /// <summary>Принимает решение по запросу</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost(RevokeRoute)]
    [ProducesResponseType<TreatmentApprovalDal>(StatusCodes.Status200OK)]
    public Task<TreatmentApprovalDal> RevokeAsync(Guid id, TreatmentActionRequest request)
    {
        return useCases.DecideTreatmentApprovalAsync(User.GetRiskActor(), id, TreatmentRules.Revoked, request);
    }
}
