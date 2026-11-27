namespace Domain.DalModels;

/// <summary>
/// Связь роли с правом
/// </summary>
public sealed record RoleRightDal
{
    /// <summary>
    /// Идентификатор записи
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Идентификатор роли
    /// </summary>
    public Guid RoleId { get; init; }

    /// <summary>
    /// Идентификатор права
    /// </summary>
    public Guid RightId { get; init; }
}
