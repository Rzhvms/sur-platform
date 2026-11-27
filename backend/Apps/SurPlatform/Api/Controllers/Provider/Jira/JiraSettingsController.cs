using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Api.Constants;
using Api.Controllers.Provider.Jira.Dto.Response;
using Api.Extensions;
using Application.Models;
using Application.UseCases.Jira.Interfaces;
using Application.UseCases.Jira.Dto.Request;
using Core.Api.Controllers.ControllerTypes;
using Domain.DalModels;
using Domain.DalModels.Jira;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Provider.Jira;

/// <summary>Администрирование Jira выбранного клиента</summary>
/// <param name="useCases">Прикладные операции Jira</param>
[Authorize]
[Route("jira")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class JiraSettingsController(IJiraUseCaseManager useCases) : BaseProviderController
{
    /// <summary>Читает настройку без секретов</summary>
    /// <returns>Текущее подключение клиента</returns>
    [HttpGet]
    [ProducesResponseType<JiraSettingsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<JiraSettingsResponse?> GetAsync()
    {
        Response.Headers.CacheControl = IdentityHttp.NoStore;
        var view = await useCases.GetJiraAdministrationAsync(User.GetRiskActor());
        return view is null ? null : JiraSettingsResponse.FromSettings(view.Settings, view);
    }

    /// <summary>Сохраняет или включает проверенную настройку</summary>
    /// <param name="request">Поля настройки</param>
    /// <param name="cancellation">Отмена проверки Jira</param>
    /// <returns>Сохранённое подключение</returns>
    [HttpPut]
    [ProducesResponseType<JiraSettingsResponse>(StatusCodes.Status200OK)]
    public async Task<JiraSettingsResponse> SaveAsync(JiraSettingsRequest request, CancellationToken cancellation)
    {
        Response.Headers.CacheControl = IdentityHttp.NoStore;
        var settings = await useCases.SaveJiraAsync(User.GetRiskActor(), request, cancellation);
        return JiraSettingsResponse.FromSettings(settings);
    }

    /// <summary>Загружает доступные проекты, типы задач или статусы</summary>
    /// <param name="request">Настройки доступа и выбранный проект</param>
    /// <param name="cancellation">Отмена запроса Jira</param>
    /// <returns>Каталоги для формы подключения</returns>
    [HttpPost("metadata")]
    [ProducesResponseType<JsonElement>(StatusCodes.Status200OK)]
    public Task<JsonElement> DiscoverAsync(JiraDiscoveryRequest request, CancellationToken cancellation)
    {
        Response.Headers.CacheControl = IdentityHttp.NoStore;
        return useCases.DiscoverJiraAsync(User.GetRiskActor(), request, cancellation);
    }

    /// <summary>Читает журнал обмена</summary>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Безопасные сведения об ошибках</returns>
    [HttpGet("errors")]
    [ProducesResponseType<JiraErrorDal[]>(StatusCodes.Status200OK)]
    public Task<JiraErrorDal[]> ErrorsAsync([Range(0, int.MaxValue)] int offset = 0,
        [Range(1, RiskRoutes.MaxPage)] int limit = RiskRoutes.DefaultPage)
    {
        Response.Headers.CacheControl = IdentityHttp.NoStore;
        return useCases.ListJiraErrorsAsync(User.GetRiskActor(), offset, limit);
    }

    /// <summary>Разрешает неопределённый итог одной попытки</summary>
    /// <param name="id">Мера</param>
    /// <param name="request">Найденная задача или подтверждение отсутствия</param>
    /// <param name="cancellation">Отмена проверки Jira</param>
    /// <returns>Состояние доставки</returns>
    [HttpPost("treatments/{id:guid}/resolve")]
    [ProducesResponseType<JiraTreatmentView>(StatusCodes.Status200OK)]
    public Task<JiraTreatmentView> ResolveAsync(Guid id, JiraResolveRequest request, CancellationToken cancellation)
    {
        return useCases.ResolveJiraAsync(User.GetRiskActor(), id, request, cancellation);
    }
}
