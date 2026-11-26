using Application.Constants;
using Application.UseCases.Organization.Dto.Request;
using System.ComponentModel.DataAnnotations;

namespace Application.UseCases.Users.Dto.Request;

/// <summary>
/// Данные для административной выдачи доступа
/// </summary>
public sealed record CreateIdentityUserRequest
{
    /// <summary>Временный пароль, передаваемый пользователю администратором</summary>
    [Required, StringLength(IdentityAccounts.MaxPasswordLength, MinimumLength = IdentityAccounts.MinPasswordLength)]
    [DataType(DataType.Password)]
    public required string TemporaryPassword { get; init; }

    /// <summary>Не включает временный пароль в диагностическое представление</summary>
    /// <returns>Имя контракта без значений полей</returns>
    public override string ToString()
    {
        return nameof(CreateIdentityUserRequest);
    }

    /// <summary>
    /// ФИО нового пользователя
    /// </summary>
    [Required, StringLength(250)]
    public required string FullName { get; init; }

    /// <summary>
    /// Адрес для входа
    /// </summary>
    [Required, EmailAddress, StringLength(254)]
    public required string Email { get; init; }

    /// <summary>
    /// Выбранные действующие роли
    /// </summary>
    [Required, MinLength(1), MaxLength(6)]
    public required Guid[] RoleIds { get; init; }

    /// <summary>
    /// Необязательные данные сотрудника и начальных назначений
    /// </summary>
    public InitialOrganizationRequest? Organization { get; init; }
}
