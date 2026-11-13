using System.ComponentModel.DataAnnotations;

namespace Application.UseCases.Users.Dto.Request;

/// <summary>
/// Активация учётной записи с выдачей временного доступа
/// </summary>
public sealed record ActivateIdentityUserRequest
{
    /// <summary>
    /// Версия прочитанной карточки
    /// </summary>
    public Guid Version { get; init; }

    /// <summary>
    /// Действующие роли для активации, если прежних недостаточно
    /// </summary>
    [MinLength(1), MaxLength(6)]
    public Guid[]? RoleIds { get; init; }
}
