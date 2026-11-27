using System.ComponentModel.DataAnnotations;

namespace Application.UseCases.Users.Dto.Request;

/// <summary>
/// Изменения учётной записи с ожидаемой версией
/// </summary>
public sealed record UpdateIdentityUserRequest
{
    /// <summary>
    /// Версия прочитанной карточки
    /// </summary>
    public Guid Version { get; init; }

    /// <summary>
    /// Новое ФИО активного пользователя
    /// </summary>
    [StringLength(250)]
    public string? FullName { get; init; }

    /// <summary>
    /// Новый адрес неактивного пользователя
    /// </summary>
    [EmailAddress, StringLength(254)]
    public string? Email { get; init; }

    /// <summary>
    /// Набор ролей активного пользователя
    /// </summary>
    [MinLength(1), MaxLength(6)]
    public Guid[]? RoleIds { get; init; }
}
