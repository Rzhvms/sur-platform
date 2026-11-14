using System.ComponentModel.DataAnnotations;
using Api.Constants;
using Api.Extensions;
using Application.Constants;
using Application.Models;
using Application.UseCases.Risks;
using Application.UseCases.Risks.Dto;
using Core.Api.Controllers.ControllerTypes;
using Domain.DalModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Public.Risks;

/// <summary>HTTP-контракт RiskCommentController</summary>
/// <param name="useCases">Прикладные операции выбранного клиента</param>
[Authorize]
[Route(RiskRoutes.Risks)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class RiskCommentController(RiskUseCaseManager useCases) : BasePublicController
{
    /// <summary>Относительный маршрут операции CommentsRoute</summary>
    private const string CommentsRoute = "{riskId:guid}/comments";

    /// <summary>Относительный маршрут операции CommentRoute</summary>
    private const string CommentRoute = CommentsRoute + "/" + RiskRoutes.ById;

    /// <summary>Возвращает видимые комментарии</summary>
    /// <param name="riskId">Родительский риск</param>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Данные операции текущего клиента</returns>
    [HttpGet(CommentsRoute)]
    public Task<RiskCommentDal[]> ListAsync(Guid riskId, [Range(0, int.MaxValue)] int offset = 0, [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        return useCases.ListCommentsAsync(User.GetRiskActor(), riskId, offset, limit);
    }

    /// <summary>Добавляет пользовательский комментарий</summary>
    /// <param name="riskId">Родительский риск</param>
    /// <param name="request">Поля команды</param>
    /// <returns>Данные операции текущего клиента</returns>
    [HttpPost(CommentsRoute)]
    [ProducesResponseType<RiskCommentDal>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RiskCommentDal>> CreateAsync(Guid riskId, RiskCommentRequest request)
    {
        return StatusCode(StatusCodes.Status201Created, await useCases.SaveCommentAsync(User.GetRiskActor(), riskId, null, request));
    }

    /// <summary>Редактирует допустимый комментарий</summary>
    /// <param name="riskId">Родительский риск</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="request">Поля команды</param>
    /// <returns>Данные операции текущего клиента</returns>
    [HttpPut(CommentRoute)]
    public Task<RiskCommentDal> EditAsync(Guid riskId, Guid id, RiskCommentRequest request)
    {
        return useCases.SaveCommentAsync(User.GetRiskActor(), riskId, id, request);
    }

    /// <summary>Скрывает комментарий и сохраняет историю</summary>
    /// <param name="riskId">Родительский риск</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="version">Ожидаемая версия</param>
    /// <returns>Данные операции текущего клиента</returns>
    [HttpDelete(CommentRoute)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAsync(Guid riskId, Guid id, [FromQuery] Guid version)
    {
        await useCases.DeleteCommentAsync(User.GetRiskActor(), riskId, id, version);
        return NoContent();
    }
}
