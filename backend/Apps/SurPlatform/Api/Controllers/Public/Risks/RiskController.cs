using System.ComponentModel.DataAnnotations;
using Api.Constants;
using Api.Extensions;
using Application.Constants;
using Application.Models;
using Application.UseCases.Risks;
using Application.UseCases.Risks.Dto;
using Application.UseCases.Risks.Dto.Request;
using Core.Api.Controllers.ControllerTypes;
using Domain.DalModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Public.Risks;

/// <summary>HTTP-контракт RiskController</summary>
/// <param name="useCases">Прикладные операции выбранного клиента</param>
[Authorize]
[Route(RiskRoutes.Risks)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class RiskController(RiskUseCaseManager useCases) : BasePublicController
{
    /// <summary>Относительный маршрут операции BoardRoute</summary>
    private const string BoardRoute = "board";

    /// <summary>Относительный маршрут операции CloseRoute</summary>
    private const string CloseRoute = RiskRoutes.ById + "/close";

    /// <summary>Относительный маршрут операции ReopenRoute</summary>
    private const string ReopenRoute = RiskRoutes.ById + "/reopen";

    /// <summary>Возвращает страницу реестра</summary>
    /// <param name="filter">Отбор и пагинация</param>
    /// <returns>Данные операции текущего клиента</returns>
    [HttpGet]
    public Task<RiskRow[]> ListAsync([FromQuery] RiskListQuery filter)
    {
        return useCases.ListAsync(User.GetRiskActor(), filter);
    }

    /// <summary>Возвращает шесть статусных колонок</summary>
    /// <param name="filter">Отбор и пагинация</param>
    /// <returns>Данные операции текущего клиента</returns>
    [HttpGet(BoardRoute)]
    public Task<RiskColumn[]> BoardAsync([FromQuery] RiskListQuery filter)
    {
        return useCases.BoardAsync(User.GetRiskActor(), filter);
    }

    /// <summary>Возвращает карточку риска</summary>
    /// <param name="id">Идентификатор записи</param>
    /// <returns>Данные операции текущего клиента</returns>
    [HttpGet(RiskRoutes.ById)]
    [ProducesResponseType<RiskCard>(StatusCodes.Status200OK)]
    public Task<RiskCard> GetAsync(Guid id)
    {
        return useCases.GetAsync(User.GetRiskActor(), id);
    }

    /// <summary>Создаёт черновик с основным процессом</summary>
    /// <param name="request">Поля команды</param>
    /// <returns>Данные операции текущего клиента</returns>
    [HttpPost]
    [ProducesResponseType<RiskDal>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RiskDal>> CreateAsync(RiskPassportRequest request)
    {
        return StatusCode(StatusCodes.Status201Created, await useCases.CreateAsync(User.GetRiskActor(), request));
    }

    /// <summary>Изменяет паспорт риска до первой оценки</summary>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Поля команды</param>
    /// <returns>Данные операции текущего клиента</returns>
    [HttpPut(RiskRoutes.ById)]
    public Task<RiskDal> UpdateAsync(Guid id, RiskPassportRequest request)
    {
        return useCases.UpdateAsync(User.GetRiskActor(), id, request);
    }

    /// <summary>Закрывает риск с указанием причины</summary>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Поля команды</param>
    /// <returns>Данные операции текущего клиента</returns>
    [HttpPost(CloseRoute)]
    [ProducesResponseType<RiskDal>(StatusCodes.Status200OK)]
    public Task<RiskDal> CloseAsync(Guid id, RiskTransitionRequest request)
    {
        return useCases.TransitionAsync(User.GetRiskActor(), id, request, close: true);
    }

    /// <summary>Возвращает обработанный риск на переоценку</summary>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Поля команды</param>
    /// <returns>Данные операции текущего клиента</returns>
    [HttpPost(ReopenRoute)]
    [ProducesResponseType<RiskDal>(StatusCodes.Status200OK)]
    public Task<RiskDal> ReopenAsync(Guid id, RiskTransitionRequest request)
    {
        return useCases.TransitionAsync(User.GetRiskActor(), id, request, close: false);
    }

    /// <summary>Относительный адрес передачи риска</summary>
    private const string TransferRoute = RiskRoutes.ById + "/transfer";

    /// <summary>Относительный адрес выбора процесса передачи</summary>
    private const string TransferProcessesRoute = TransferRoute + "/processes";

    /// <summary>Относительный адрес истории пересмотров</summary>
    private const string ReviewsRoute = RiskRoutes.ById + "/reviews";

    /// <summary>Передаёт риск новому основному процессу</summary>
    /// <param name="id">Риск</param>
    /// <param name="request">Версия и целевой процесс</param>
    /// <returns>Риск в ожидании переоценки</returns>
    [HttpPost(TransferRoute)]
    [ProducesResponseType<RiskDal>(StatusCodes.Status200OK)]
    public Task<RiskDal> TransferAsync(Guid id, TransferRiskRequest request)
    {
        return useCases.TransferRiskAsync(User.GetRiskActor(), id, request);
    }

    /// <summary>Возвращает доступные новые процессы для передачи</summary>
    /// <param name="id">Риск</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Доступные новые процессы</returns>
    [HttpGet(TransferProcessesRoute)]
    [ProducesResponseType<RiskProcessOption[]>(StatusCodes.Status200OK)]
    public Task<RiskProcessOption[]> TransferProcessesAsync(Guid id, [Range(0, int.MaxValue)] int offset = 0,
        [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        return useCases.ListTransferProcessesAsync(User.GetRiskActor(), id, offset, limit);
    }

    /// <summary>Читает историю запусков и завершений пересмотра</summary>
    /// <param name="id">Риск</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Страница истории пересмотра</returns>
    [HttpGet(ReviewsRoute)]
    [ProducesResponseType<RiskReviewView[]>(StatusCodes.Status200OK)]
    public Task<RiskReviewView[]> ReviewsAsync(Guid id, [Range(0, int.MaxValue)] int offset = 0,
        [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        return useCases.ListRiskReviewsAsync(User.GetRiskActor(), id, offset, limit);
    }
}
