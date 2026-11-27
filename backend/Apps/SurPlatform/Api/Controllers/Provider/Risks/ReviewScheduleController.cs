using System.ComponentModel.DataAnnotations;
using Api.Constants;
using Api.Extensions;
using Api.Controllers.Provider.Risks.Dto.Response;
using Application.UseCases.Risks.Interfaces;
using Application.UseCases.Risks.Dto.Request;
using Core.Api.Controllers.ControllerTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Provider.Risks;

/// <summary>Административные команды календарного пересмотра</summary>
/// <param name="useCases">Прикладные операции выбранной базы</param>
[Authorize]
[Route("review-schedules")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class ReviewScheduleController(IReviewUseCaseManager useCases) : BaseProviderController
{
    /// <summary>Читает календарные правила</summary>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Результат операции выбранного клиента</returns>
    [HttpGet]
    [ProducesResponseType<ReviewScheduleResponse[]>(StatusCodes.Status200OK)]
    public async Task<ReviewScheduleResponse[]> ListAsync([Range(0, int.MaxValue)] int offset = 0, [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        var schedules = await useCases.ListReviewSchedulesAsync(User.GetRiskActor(), offset, limit);
        return Array.ConvertAll(schedules, ReviewScheduleResponse.FromDal);
    }

    /// <summary>Создаёт календарное правило</summary>
    /// <param name="request">Поля формы</param>
    /// <returns>Результат операции выбранного клиента</returns>
    [HttpPost]
    [ProducesResponseType<ReviewScheduleResponse>(StatusCodes.Status200OK)]
    public async Task<ReviewScheduleResponse> CreateAsync(ReviewScheduleRequest request)
    {
        var schedule = await useCases.SaveReviewScheduleAsync(User.GetRiskActor(), null, request);
        return ReviewScheduleResponse.FromDal(schedule);
    }

    /// <summary>Изменяет календарное правило и его активность</summary>
    /// <param name="id">Идентификатор настройки</param>
    /// <param name="request">Поля формы</param>
    /// <returns>Результат операции выбранного клиента</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<ReviewScheduleResponse>(StatusCodes.Status200OK)]
    public async Task<ReviewScheduleResponse> UpdateAsync(Guid id, ReviewScheduleRequest request)
    {
        var schedule = await useCases.SaveReviewScheduleAsync(User.GetRiskActor(), id, request);
        return ReviewScheduleResponse.FromDal(schedule);
    }

    /// <summary>Удаляет источник календарного пересмотра</summary>
    /// <param name="id">Идентификатор настройки</param>
    /// <param name="version">Ожидаемая версия</param>
    /// <returns>Результат операции выбранного клиента</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAsync(Guid id, [FromQuery] Guid version)
    {
        await useCases.DeleteReviewScheduleAsync(User.GetRiskActor(), id, version);
        return NoContent();
    }
}
