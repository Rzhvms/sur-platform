using Application.Constants;
using Application.Models;
using Application.UseCases.Users.Interfaces;
using Application.UseCases.Users.Dto.Request;
using Core.Api.Controllers.ControllerTypes;
using Core.Api.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Provider.Users;

/// <summary>
/// Чтение системного каталога и изменение матрицы прав
/// </summary>
/// <param name="useCases">Хранилище матрицы клиента</param>
[Route("roles")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class ProviderRoleController(IIdentityAdministrationUseCaseManager useCases) : BaseProviderController
{
    /// <summary>
    /// Возвращает шесть системных ролей с их правами
    /// </summary>
    /// <returns>Роли и текущие версии матрицы</returns>
    [HttpGet]
    public Task<IdentityRole[]> ListAsync()
    {
        return useCases.RolesAsync();
    }

    /// <summary>
    /// Возвращает каталог допустимых прав
    /// </summary>
    /// <returns>Системные права клиента</returns>
    [HttpGet("rights")]
    public Task<IdentityRight[]> RightsAsync()
    {
        return useCases.RightsAsync();
    }

    /// <summary>
    /// Обновляет системные роли и права до версии бэкенда
    /// </summary>
    /// <returns>204 после успешной синхронизации каталога текущего клиента</returns>
    [HttpPut("catalog")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SynchronizeCatalogAsync()
    {
        await useCases.SynchronizeCatalogAsync(User.GetUserId(),
            Guid.Parse(User.FindFirst(AccessTokenClaimTypes.SessionId)?.Value ?? string.Empty));
        return NoContent();
    }

    /// <summary>
    /// Сохраняет состояние роли и полный набор её прав атомарно
    /// </summary>
    /// <param name="roleId">Изменяемая роль</param>
    /// <param name="request">Ожидаемая версия, состояние и права</param>
    /// <returns>Обновлённая матрица роли</returns>
    [HttpPut("{roleId:guid}")]
    public Task<IdentityRole> SaveAsync(Guid roleId, UpdateIdentityRoleRequest request)
    {
        return useCases.SaveRoleAsync(User.GetUserId(),
            Guid.Parse(User.FindFirst(AccessTokenClaimTypes.SessionId)?.Value ?? string.Empty), roleId, request);
    }
}
