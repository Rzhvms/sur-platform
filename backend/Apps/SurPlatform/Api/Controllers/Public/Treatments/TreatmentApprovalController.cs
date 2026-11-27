using System.ComponentModel.DataAnnotations;
using Api.Constants;
using Api.Extensions;
using Application.Constants;
using Application.Models;
using Application.UseCases.Treatments.Interfaces;
using Application.UseCases.Treatments.Dto.Request;
using Core.Api.Controllers.ControllerTypes;
using Domain.DalModels;
using Domain.DalModels.Treatments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Public.Treatments;

/// <summary>HTTP-контракт операций с мерами</summary>
/// <param name="useCases">Операции текущего клиента</param>
[Authorize]
[Route("treatment-approvals")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class TreatmentApprovalController(ITreatmentUseCaseManager useCases) : BasePublicController
{
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

    /// <summary>Принимает решение по запросу</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType<TreatmentApprovalDal>(StatusCodes.Status200OK)]
    public Task<TreatmentApprovalDal> ApproveAsync(Guid id, TreatmentActionRequest request)
    {
        return useCases.DecideTreatmentApprovalAsync(User.GetRiskActor(), id, TreatmentRules.Approved, request);
    }

    /// <summary>Принимает решение по запросу</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<TreatmentApprovalDal>(StatusCodes.Status200OK)]
    public Task<TreatmentApprovalDal> RejectAsync(Guid id, TreatmentActionRequest request)
    {
        return useCases.DecideTreatmentApprovalAsync(User.GetRiskActor(), id, TreatmentRules.Rejected, request);
    }

    /// <summary>Принимает решение по запросу</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost("{id:guid}/revoke")]
    [ProducesResponseType<TreatmentApprovalDal>(StatusCodes.Status200OK)]
    public Task<TreatmentApprovalDal> RevokeAsync(Guid id, TreatmentActionRequest request)
    {
        return useCases.DecideTreatmentApprovalAsync(User.GetRiskActor(), id, TreatmentRules.Revoked, request);
    }
}
