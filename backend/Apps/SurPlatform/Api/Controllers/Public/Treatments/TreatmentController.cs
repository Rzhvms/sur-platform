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
public sealed class TreatmentController(TreatmentUseCaseManager useCases) : BasePublicController
{
    /// <summary>Базовый маршрут контроллера</summary>
    private const string TreatmentRoute = "treatments";

    /// <summary>Маршрут операции Get</summary>
    private const string GetRoute = "{id:guid}";

    /// <summary>Читает карточку меры</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <returns>Результат операции</returns>
    [HttpGet(GetRoute)]
    [ProducesResponseType<TreatmentCard>(StatusCodes.Status200OK)]
    public Task<TreatmentCard> GetAsync(Guid id)
    {
        return useCases.GetTreatmentAsync(User.GetRiskActor(), id);
    }

    /// <summary>Маршрут операции Update</summary>
    private const string UpdateRoute = "{id:guid}";

    /// <summary>Создаёт или сохраняет черновик</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPut(UpdateRoute)]
    [ProducesResponseType<TreatmentDal>(StatusCodes.Status200OK)]
    public Task<TreatmentDal> UpdateAsync(Guid id, TreatmentDraftRequest request)
    {
        return useCases.SaveTreatmentDraftAsync(User.GetRiskActor(), id, false, request);
    }

    /// <summary>Маршрут операции Dispatch</summary>
    private const string DispatchRoute = "{id:guid}/dispatch";

    /// <summary>Назначает исполнителя или передаёт меру к выполнению</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost(DispatchRoute)]
    [ProducesResponseType<TreatmentDal>(StatusCodes.Status200OK)]
    public Task<TreatmentDal> DispatchAsync(Guid id, TreatmentActionRequest request)
    {
        return useCases.AssignTreatmentAsync(User.GetRiskActor(), id, true, request);
    }

    /// <summary>Маршрут операции Assign</summary>
    private const string AssignRoute = "{id:guid}/assignment";

    /// <summary>Назначает исполнителя или передаёт меру к выполнению</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPut(AssignRoute)]
    [ProducesResponseType<TreatmentDal>(StatusCodes.Status200OK)]
    public Task<TreatmentDal> AssignAsync(Guid id, TreatmentActionRequest request)
    {
        return useCases.AssignTreatmentAsync(User.GetRiskActor(), id, false, request);
    }

    /// <summary>Маршрут операции Executors</summary>
    private const string ExecutorsRoute = "{id:guid}/executors";

    /// <summary>Читает допустимых исполнителей</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции</returns>
    [HttpGet(ExecutorsRoute)]
    [ProducesResponseType<TreatmentExecutorOption[]>(StatusCodes.Status200OK)]
    public Task<TreatmentExecutorOption[]> ExecutorsAsync(Guid id, [Range(0, int.MaxValue)] int offset = 0, [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        return useCases.ListTreatmentExecutorsAsync(User.GetRiskActor(), id, offset, limit);
    }

    /// <summary>Маршрут операции Submit</summary>
    private const string SubmitRoute = "{id:guid}/approvals";

    /// <summary>Отправляет меру на согласование</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost(SubmitRoute)]
    [ProducesResponseType<TreatmentApprovalDal>(StatusCodes.Status200OK)]
    public Task<TreatmentApprovalDal> SubmitAsync(Guid id, TreatmentActionRequest request)
    {
        return useCases.SubmitTreatmentAsync(User.GetRiskActor(), id, request);
    }

    /// <summary>Маршрут операции Hold</summary>
    private const string HoldRoute = "{id:guid}/hold";

    /// <summary>Выполняет ручной переход</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost(HoldRoute)]
    [ProducesResponseType<TreatmentDal>(StatusCodes.Status200OK)]
    public Task<TreatmentDal> HoldAsync(Guid id, TreatmentActionRequest request)
    {
        return useCases.TransitionTreatmentAsync(User.GetRiskActor(), id, TreatmentRules.Hold, request);
    }

    /// <summary>Маршрут операции Resume</summary>
    private const string ResumeRoute = "{id:guid}/resume";

    /// <summary>Выполняет ручной переход</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost(ResumeRoute)]
    [ProducesResponseType<TreatmentDal>(StatusCodes.Status200OK)]
    public Task<TreatmentDal> ResumeAsync(Guid id, TreatmentActionRequest request)
    {
        return useCases.TransitionTreatmentAsync(User.GetRiskActor(), id, TreatmentRules.Working, request);
    }

    /// <summary>Маршрут операции Complete</summary>
    private const string CompleteRoute = "{id:guid}/complete";

    /// <summary>Выполняет ручной переход</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost(CompleteRoute)]
    [ProducesResponseType<TreatmentDal>(StatusCodes.Status200OK)]
    public Task<TreatmentDal> CompleteAsync(Guid id, TreatmentActionRequest request)
    {
        return useCases.TransitionTreatmentAsync(User.GetRiskActor(), id, TreatmentRules.Done, request);
    }

    /// <summary>Маршрут операции Cancel</summary>
    private const string CancelRoute = "{id:guid}/cancel";

    /// <summary>Выполняет ручной переход</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost(CancelRoute)]
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
    [HttpGet(SubmitRoute)]
    [ProducesResponseType<TreatmentApprovalDal[]>(StatusCodes.Status200OK)]
    public Task<TreatmentApprovalDal[]> ApprovalHistoryAsync(Guid id, [Range(0, int.MaxValue)] int offset = 0,
        [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        return useCases.ListTreatmentApprovalHistoryAsync(User.GetRiskActor(), id, offset, limit);
    }
}
