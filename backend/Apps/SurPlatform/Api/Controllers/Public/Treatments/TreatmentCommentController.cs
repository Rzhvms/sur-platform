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
public sealed class TreatmentCommentController(TreatmentUseCaseManager useCases) : BasePublicController
{
    /// <summary>Базовый маршрут контроллера</summary>
    private const string TreatmentRoute = "treatments/{id:guid}/comments";

    /// <summary>Читает комментарии меры</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции</returns>
    [HttpGet]
    [ProducesResponseType<TreatmentCommentDal[]>(StatusCodes.Status200OK)]
    public Task<TreatmentCommentDal[]> ListAsync(Guid id, [Range(0, int.MaxValue)] int offset = 0, [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        return useCases.ListTreatmentCommentsAsync(User.GetRiskActor(), id, offset, limit);
    }

    /// <summary>Создаёт, редактирует или удаляет комментарий</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPost]
    [ProducesResponseType<TreatmentCommentDal>(StatusCodes.Status200OK)]
    public Task<TreatmentCommentDal> CreateAsync(Guid id, TreatmentCommentRequest request)
    {
        return useCases.SaveTreatmentCommentAsync(User.GetRiskActor(), id, null, request, false);
    }

    /// <summary>Маршрут операции Edit</summary>
    private const string EditRoute = "{commentId:guid}";

    /// <summary>Создаёт, редактирует или удаляет комментарий</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="commentId">Комментарий</param>
    /// <param name="request">Данные команды</param>
    /// <returns>Результат операции</returns>
    [HttpPut(EditRoute)]
    [ProducesResponseType<TreatmentCommentDal>(StatusCodes.Status200OK)]
    public Task<TreatmentCommentDal> EditAsync(Guid id, Guid commentId, TreatmentCommentRequest request)
    {
        return useCases.SaveTreatmentCommentAsync(User.GetRiskActor(), id, commentId, request, false);
    }

    /// <summary>Маршрут операции Delete</summary>
    private const string DeleteRoute = "{commentId:guid}";

    /// <summary>Создаёт, редактирует или удаляет комментарий</summary>
    /// <param name="id">Идентификатор меры или запроса</param>
    /// <param name="commentId">Комментарий</param>
    /// <param name="version">Версия комментария</param>
    /// <returns>Результат операции</returns>
    [HttpDelete(DeleteRoute)]
    [ProducesResponseType<TreatmentCommentDal>(StatusCodes.Status200OK)]
    public Task<TreatmentCommentDal> DeleteAsync(Guid id, Guid commentId, [FromQuery] Guid version)
    {
        return useCases.SaveTreatmentCommentAsync(User.GetRiskActor(), id, commentId, new TreatmentCommentRequest { Version = version }, true);
    }
}
