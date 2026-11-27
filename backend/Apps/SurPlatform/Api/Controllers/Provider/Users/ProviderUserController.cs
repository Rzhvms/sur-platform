using Application.UseCases.Organization;
using Application.Constants;
using Application.Models;
using Application.UseCases.Users.Dto.Response;
using Application.UseCases.Users;
using Application.UseCases.Users.Dto.Request;
using Core.Api.Controllers.ControllerTypes;
using Core.Api.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Provider.Users;

/// <summary>
/// Административные операции с интерактивными пользователями
/// </summary>
/// <param name="organization">Чтение организационных зависимостей пользователя</param>
/// <param name="useCases">Запросы и команды учётных записей</param>
[Route("users")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class ProviderUserController(IdentityAdministrationUseCaseManager useCases, OrganizationUseCaseManager organization)
    : BaseProviderController
{
    /// <summary>
    /// Размер страницы по умолчанию
    /// </summary>
    private const int DefaultPageSize = 50;

    /// <summary>
    /// Верхняя граница страницы административного списка
    /// </summary>
    private const int MaxPageSize = 100;

    /// <summary>
    /// Возвращает страницу пользователей в порядке ID
    /// </summary>
    /// <param name="after">Идентификатор последней строки предыдущей страницы</param>
    /// <param name="limit">Размер запрашиваемой страницы</param>
    /// <returns>Страница пользователей</returns>
    [HttpGet]
    public async Task<ActionResult<IdentityUser[]>> ListAsync(Guid after, int limit = DefaultPageSize)
    {
        if (limit is < 1 or > MaxPageSize)
        {
            return BadRequest();
        }

        return await useCases.ListAsync(after, limit);
    }

    /// <summary>
    /// Возвращает страницу в формате существующего списка пользователей
    /// </summary>
    /// <param name="after">Последний идентификатор предыдущей страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Список пользователей с прежней формой ответа</returns>
    [HttpGet("list")]
    public async Task<ActionResult<GetUserListResponse>> LegacyListAsync(Guid after, int limit = DefaultPageSize)
    {
        if (limit is < 1 or > MaxPageSize)
        {
            return BadRequest();
        }

        var users = await useCases.ListAsync(after, limit);
        return new GetUserListResponse
        {
            UserList = users.Select(user => new GetUserResponse
            {
                Id = user.Id, Email = user.Email ?? string.Empty, RoleId = user.RoleId, FullName = user.FullName, IsActive = user.IsActive,
                FirstName = user.FirstName, LastName = user.LastName, Patronymic = user.Patronymic
            }).ToList()
        };
    }

    /// <summary>
    /// Возвращает карточку интерактивного пользователя
    /// </summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <returns>Карточка или 404</returns>
    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<IdentityUser>> GetAsync(Guid userId)
    {
        var user = await useCases.FindAsync(userId);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>
    /// Создаёт пользователя с временным доступом и выбранными ролями
    /// </summary>
    /// <param name="request">Данные нового пользователя</param>
    /// <returns>Созданная учётная запись с временным доступом</returns>
    [HttpPost]
    [ProducesResponseType<IdentityUser>(StatusCodes.Status201Created)]
    public async Task<ActionResult<IdentityUser>> CreateAsync(CreateIdentityUserRequest request)
    {
        return StatusCode(StatusCodes.Status201Created, await useCases.CreateAsync(User.GetUserId(), SessionId, request));
    }

    /// <summary>
    /// Сохраняет разрешённые поля с проверкой ожидаемой версии
    /// </summary>
    /// <param name="userId">Изменяемая запись</param>
    /// <param name="request">Поля и версия карточки</param>
    /// <returns>Обновлённая запись</returns>
    [HttpPatch("{userId:guid}")]
    public Task<IdentityUser> UpdateAsync(Guid userId, UpdateIdentityUserRequest request)
    {
        return useCases.UpdateAsync(User.GetUserId(), SessionId, userId, request);
    }

    /// <summary>
    /// Активирует учётную запись и выдаёт новый временный пароль
    /// </summary>
    /// <param name="userId">Активируемая запись</param>
    /// <param name="request">Версия карточки и при необходимости набор ролей</param>
    /// <returns>Обновлённая учётная запись</returns>
    [HttpPost("{userId:guid}/activate")]
    public Task<IdentityUser> ActivateAsync(Guid userId, ActivateIdentityUserRequest request)
    {
        return useCases.ReissueAsync(User.GetUserId(), SessionId, userId, request, request.Version, request.TemporaryPassword);
    }

    /// <summary>
    /// Заменяет пароль временным и отзывает прежние сессии
    /// </summary>
    /// <param name="userId">Получатель доступа</param>
    /// <param name="request">Версия карточки и новый временный пароль</param>
    /// <returns>Обновлённая учётная запись</returns>
    [HttpPost("{userId:guid}/reissue-access")]
    public Task<IdentityUser> ReissueAsync(Guid userId, IssueIdentityAccessRequest request)
    {
        return useCases.ReissueAsync(User.GetUserId(), SessionId, userId, null, request.Version, request.TemporaryPassword);
    }

    /// <summary>Читает организационные зависимости для подтверждения деактивации</summary>
    /// <param name="userId">Учётная запись</param>
    /// <returns>Действующий сотрудник, владение и назначения</returns>
    [HttpGet("{userId:guid}/organization")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganizationUserContext>> GetOrganizationAsync(Guid userId)
    {
        if (await useCases.FindAsync(userId) is null)
        {
            return NotFound();
        }

        return await organization.GetUserContextAsync(userId);
    }

    /// <summary>Отключает вход и активную запись сотрудника после проверки зависимостей</summary>
    /// <param name="userId">Отключаемый пользователь</param>
    /// <param name="request">Версия карточки</param>
    /// <returns>Деактивированная учётная запись</returns>
    [HttpPost("{userId:guid}/deactivate")]
    public Task<IdentityUser> DeactivateAsync(Guid userId, DeactivateIdentityUserRequest request)
    {
        return useCases.DeactivateAsync(User.GetUserId(), SessionId, userId, request.Version);
    }

    /// <summary>
    /// Возвращает 410 для снятого маршрута физического удаления
    /// </summary>
    /// <returns>Сообщение о закрытом контракте</returns>
    [HttpDelete("{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public IActionResult Delete()
    {
        return Problem(statusCode: StatusCodes.Status410Gone);
    }

    /// <summary>
    /// Идентификатор сессии из проверенного access-токена
    /// </summary>
    private Guid SessionId => Guid.Parse(User.FindFirst(AccessTokenClaimTypes.SessionId)?.Value ?? string.Empty);
}
