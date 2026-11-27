using Application.Constants;
using System.ComponentModel.DataAnnotations;

namespace Application.UseCases.Users.Dto.Request;

/// <summary>
/// Активация учётной записи с выдачей временного доступа
/// </summary>
public sealed record ActivateIdentityUserRequest
{
    /// <summary>Временный пароль, передаваемый пользователю администратором</summary>
    [Required, StringLength(IdentityAccounts.MaxPasswordLength, MinimumLength = IdentityAccounts.MinPasswordLength)]
    [DataType(DataType.Password)]
    public required string TemporaryPassword { get; init; }

    /// <summary>Не включает временный пароль в диагностическое представление</summary>
    /// <returns>Имя контракта без значений полей</returns>
    public override string ToString()
    {
        return nameof(ActivateIdentityUserRequest);
    }

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
