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
}