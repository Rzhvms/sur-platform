using Application.Models;
using Application.UseCases.Users.Dto.Request;
using Application.UseCases.Users.Dto.Response;

namespace Application.UseCases.Users.Interfaces;

/// <summary>
/// UseCase сценарии по работе с данными пользователей
/// </summary>
public interface IUserUseCaseManager
{
    /// <summary>Читает эффективные права текущего пользователя</summary>
    /// <param name="actor">Пользователь и версия доступа из проверенной сессии</param>
    /// <returns>Профиль доступа для навигации</returns>
    Task<CurrentUserAccessResponse> GetCurrentAccessAsync(RiskActor actor);

    /// <summary>
    /// Изменение пароля
    /// </summary>
    Task<ChangeUserPasswordResponse> ChangePasswordAsync(Guid id, ChangeUserPasswordRequest request);
}
