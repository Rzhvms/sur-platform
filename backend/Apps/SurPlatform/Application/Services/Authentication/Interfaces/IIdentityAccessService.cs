using Application.Models;
using Application.UseCases.Users.Dto.Request;

namespace Application.Services.Authentication.Interfaces;

/// <summary>
/// Создание и повторная выдача временного доступа пользователю
/// </summary>
public interface IIdentityAccessService
{
    /// <summary>Создаёт пользователя, роли и начальные оргназначения с временным доступом</summary>
    /// <param name="actor">Администратор</param>
    /// <param name="sessionId">Сессия администратора</param>
    /// <param name="request">Данные и временный пароль</param>
    /// <returns>Созданная карточка без исходного пароля</returns>
    Task<IdentityUser> CreateAsync(Guid actor, Guid sessionId, CreateIdentityUserRequest request);

    /// <summary>Заменяет временный пароль и при необходимости активирует пользователя</summary>
    /// <param name="actor">Администратор</param>
    /// <param name="sessionId">Сессия администратора</param>
    /// <param name="userId">Получатель доступа</param>
    /// <param name="activation">Параметры активации или отсутствие активации</param>
    /// <param name="version">Ожидаемая версия карточки</param>
    /// <param name="temporaryPassword">Пароль для самостоятельной передачи пользователю</param>
    /// <returns>Карточка без исходного пароля</returns>
    Task<IdentityUser> ReissueAsync(Guid actor, Guid sessionId, Guid userId,
        ActivateIdentityUserRequest? activation, Guid version, string temporaryPassword);

    /// <summary>Настраивает первоначальный локальный административный доступ</summary>
    /// <param name="email">Адрес администратора</param>
    /// <param name="fullName">ФИО администратора</param>
    /// <param name="password">Пароль из доверенной конфигурации</param>
    /// <param name="reissue">Явная повторная выдача оператором</param>
    /// <returns>Результат настройки или отсутствие изменения</returns>
    Task<string?> BootstrapAsync(string email, string fullName, string password, bool reissue);
}
