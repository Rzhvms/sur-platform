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
[Route("treatments")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class TreatmentController(ITreatmentUseCaseManager useCases) : BasePublicController
{
    /// <summary>Читает общий список мер текущего клиента</summary>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Страница мер со связанными названиями</returns>
    [HttpGet]
    [ProducesResponseType<TreatmentRegistry>(StatusCodes.Status200OK)]
    public Task<TreatmentRegistry> ListAsync([Range(0, int.MaxValue)] int offset = 0,
        [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        return useCases.ListTreatmentRegistryAsync(User.GetRiskActor(), offset, limit);
    }

    /// <summary>Читает карточку меры</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <returns>Результат операции</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TreatmentCard>(StatusCodes.Status200OK)]
    public Task<TreatmentCard> GetAsync(Guid id)
    {
        return useCases.GetTreatmentAsync(User.GetRiskActor(), id);
    }

    /// <summary>Создаёт или сохраняет черновик</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<TreatmentDal>(StatusCodes.Status200OK)]
    public Task<TreatmentDal> UpdateAsync(Guid id, TreatmentDraftRequest request)
    {
        return useCases.SaveTreatmentDraftAsync(User.GetRiskActor(), id, false, request);
    }

    /// <summary>Назначает исполнителя или передаёт меру к выполнению</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost("{id:guid}/dispatch")]
    [ProducesResponseType<TreatmentDal>(StatusCodes.Status200OK)]
    public Task<TreatmentDal> DispatchAsync(Guid id, TreatmentActionRequest request)
    {
        return useCases.AssignTreatmentAsync(User.GetRiskActor(), id, true, request);
    }

    /// <summary>Назначает исполнителя или передаёт меру к выполнению</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPut("{id:guid}/assignment")]
    [ProducesResponseType<TreatmentDal>(StatusCodes.Status200OK)]
    public Task<TreatmentDal> AssignAsync(Guid id, TreatmentActionRequest request)
    {
        return useCases.AssignTreatmentAsync(User.GetRiskActor(), id, false, request);
    }

    /// <summary>Читает допустимых исполнителей</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции</returns>
    [HttpGet("{id:guid}/executors")]
    [ProducesResponseType<TreatmentExecutorOption[]>(StatusCodes.Status200OK)]
    public Task<TreatmentExecutorOption[]> ExecutorsAsync(Guid id, [Range(0, int.MaxValue)] int offset = 0, [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        return useCases.ListTreatmentExecutorsAsync(User.GetRiskActor(), id, offset, limit);
    }

    /// <summary>Отправляет меру на согласование</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost("{id:guid}/approvals")]
    [ProducesResponseType<TreatmentApprovalDal>(StatusCodes.Status200OK)]
    public Task<TreatmentApprovalDal> SubmitAsync(Guid id, TreatmentActionRequest request)
    {
        return useCases.SubmitTreatmentAsync(User.GetRiskActor(), id, request);
    }

    /// <summary>Выполняет ручной переход</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost("{id:guid}/hold")]
    [ProducesResponseType<TreatmentDal>(StatusCodes.Status200OK)]
    public Task<TreatmentDal> HoldAsync(Guid id, TreatmentActionRequest request)
    {
        return useCases.TransitionTreatmentAsync(User.GetRiskActor(), id, TreatmentRules.Hold, request);
    }

    /// <summary>Выполняет ручной переход</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost("{id:guid}/resume")]
    [ProducesResponseType<TreatmentDal>(StatusCodes.Status200OK)]
    public Task<TreatmentDal> ResumeAsync(Guid id, TreatmentActionRequest request)
    {
        return useCases.TransitionTreatmentAsync(User.GetRiskActor(), id, TreatmentRules.Working, request);
    }

    /// <summary>Выполняет ручной переход</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType<TreatmentDal>(StatusCodes.Status200OK)]
    public Task<TreatmentDal> CompleteAsync(Guid id, TreatmentActionRequest request)
    {
        return useCases.TransitionTreatmentAsync(User.GetRiskActor(), id, TreatmentRules.Done, request);
    }

    /// <summary>Выполняет ручной переход</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<TreatmentDal>(StatusCodes.Status200OK)]
    public Task<TreatmentDal> CancelAsync(Guid id, TreatmentActionRequest request)
    {
        return useCases.TransitionTreatmentAsync(User.GetRiskActor(), id, TreatmentRules.Cancelled, request);
    }

    /// <summary>Возвращает историю запросов согласования меры</summary>
    /// <param name="id">Мера</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Запросы с исходным решением и отзывом</returns>
    [HttpGet("{id:guid}/approvals")]
    [ProducesResponseType<TreatmentApprovalDal[]>(StatusCodes.Status200OK)]
    public Task<TreatmentApprovalDal[]> ApprovalHistoryAsync(Guid id, [Range(0, int.MaxValue)] int offset = 0,
        [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        return useCases.ListTreatmentApprovalHistoryAsync(User.GetRiskActor(), id, offset, limit);
    }
}
