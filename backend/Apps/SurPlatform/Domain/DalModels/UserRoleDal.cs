namespace Domain.DalModels;

/// <summary>
/// Связь пользователя с назначенной ролью
/// </summary>
public sealed record UserRoleDal
{
    /// <summary>
    /// Идентификатор пользователя
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Идентификатор роли
    /// </summary>
    public Guid RoleId { get; init; }

    /// <summary>
    /// Признак действующей записи
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Пользователь или системный субъект, изменивший запись
    /// </summary>
    public Guid? UpdatedBy { get; init; }

    /// <summary>
    /// Момент изменения записи
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}
