using System.ComponentModel.DataAnnotations;
using Api.Constants;
using Api.Extensions;
using Application.Models;
using Application.UseCases.Risks;
using Application.UseCases.Risks.Dto.Request;
using Core.Api.Controllers.ControllerTypes;
using Domain.DalModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Provider.Risks;

/// <summary>Административные команды внешних источников пересмотра</summary>
/// <param name="useCases">Прикладные операции выбранной базы</param>
[Authorize]
[Route(SettingsRoute)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class ReviewWebhookSettingsController(ReviewUseCaseManager useCases) : BaseProviderController
{
    /// <summary>Маршрут источников пересмотра</summary>
    private const string SettingsRoute = "review-webhooks";

    /// <summary>Команда подтверждённой замены секрета</summary>
    private const string RotateRoute = RiskRoutes.ById + "/rotate-token";

    /// <summary>Читает настройки внешних источников</summary>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции выбранного клиента</returns>
    [HttpGet]
    [ProducesResponseType<ReviewWebhookDal[]>(StatusCodes.Status200OK)]
    public Task<ReviewWebhookDal[]> ListAsync([Range(0, int.MaxValue)] int offset = 0, [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        return useCases.ListReviewWebhooksAsync(User.GetRiskActor(), offset, limit);
    }

    /// <summary>Читает внешний источник и связанные риски</summary>
    /// <param name="id">Идентификатор настройки</param>
    /// <returns>Результат операции выбранного клиента</returns>
    [HttpGet(RiskRoutes.ById)]
    [ProducesResponseType<ReviewWebhookView>(StatusCodes.Status200OK)]
    public Task<ReviewWebhookView> GetAsync(Guid id)
    {
        return useCases.GetReviewWebhookAsync(User.GetRiskActor(), id);
    }

    /// <summary>Регистрирует внешний источник и выдаёт секрет один раз</summary>
    /// <param name="request">Поля формы</param>
    /// <returns>Результат операции выбранного клиента</returns>
    [HttpPost]
    [ProducesResponseType<ReviewWebhookReceipt>(StatusCodes.Status200OK)]
    public Task<ReviewWebhookReceipt> CreateAsync(ReviewWebhookRequest request)
    {
        Response.Headers.CacheControl = IdentityHttp.NoStore;
        return useCases.CreateReviewWebhookAsync(User.GetRiskActor(), request);
    }

    /// <summary>Изменяет внешнюю настройку, область и активность</summary>
    /// <param name="id">Идентификатор настройки</param>
    /// <param name="request">Поля формы</param>
    /// <returns>Результат операции выбранного клиента</returns>
    [HttpPut(RiskRoutes.ById)]
    [ProducesResponseType<ReviewWebhookView>(StatusCodes.Status200OK)]
    public Task<ReviewWebhookView> UpdateAsync(Guid id, ReviewWebhookRequest request)
    {
        return useCases.UpdateReviewWebhookAsync(User.GetRiskActor(), id, request);
    }

    /// <summary>Заменяет Bearer-секрет и отзывает предыдущий</summary>
    /// <param name="id">Идентификатор настройки</param>
    /// <param name="request">Поля формы</param>
    /// <returns>Результат операции выбранного клиента</returns>
    [HttpPost(RotateRoute)]
    [ProducesResponseType<ReviewWebhookReceipt>(StatusCodes.Status200OK)]
    public Task<ReviewWebhookReceipt> RotateAsync(Guid id, RotateReviewTokenRequest request)
    {
        Response.Headers.CacheControl = IdentityHttp.NoStore;
        return useCases.RotateReviewWebhookAsync(User.GetRiskActor(), id, request);
    }

    /// <summary>Удаляет внешний источник и его действующие связи</summary>
    /// <param name="id">Идентификатор настройки</param>
    /// <param name="version">Ожидаемая версия</param>
    /// <returns>Результат операции выбранного клиента</returns>
    [HttpDelete(RiskRoutes.ById)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAsync(Guid id, [FromQuery] Guid version)
    {
        await useCases.DeleteReviewWebhookAsync(User.GetRiskActor(), id, version);
        return NoContent();
    }
}
