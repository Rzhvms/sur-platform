using System.ComponentModel.DataAnnotations;

namespace Application.UseCases.Users.Dto.Request;

/// <summary>
/// Сохранение состояния роли и её матрицы
/// </summary>
public sealed record UpdateIdentityRoleRequest
{
    /// <summary>
    /// Версия прочитанной роли
    /// </summary>
    public Guid Version { get; init; }

    /// <summary>
    /// Требуемая активность роли
    /// </summary>
    public required bool IsActive { get; init; }

    /// <summary>
    /// Полный список назначаемых прав
    /// </summary>
    [Required, MaxLength(23)]
    public required Guid[] RightIds { get; init; }
}
