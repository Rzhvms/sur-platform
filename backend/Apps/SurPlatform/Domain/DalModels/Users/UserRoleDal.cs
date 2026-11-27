namespace Domain.DalModels.Users;

/// <summary>
/// Назначение роли пользователю с сохранением состояния и автора изменения
/// </summary>
public sealed record UserRoleDal
{
    /// <summary>
    /// Идентификатор пользователя, которому назначена роль
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Идентификатор назначенной роли
    /// </summary>
    public Guid RoleId { get; init; }

    /// <summary>
    /// Признак действующего назначения роли пользователю
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Идентификатор пользователя или системного субъекта, последним изменившего назначение
    /// </summary>
    public Guid? UpdatedBy { get; init; }

    /// <summary>
    /// Время последнего изменения назначения роли в UTC
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}