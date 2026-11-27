using Domain.DalModels.Base;

namespace Domain.DalModels.Rights;

/// <summary>
/// Назначение права роли в матрице доступа
/// </summary>
public sealed record RoleRightDal : DalModelBase
{
    /// <summary>
    /// Идентификатор роли, которой назначено право
    /// </summary>
    public Guid RoleId { get; init; }

    /// <summary>
    /// Идентификатор назначенного права
    /// </summary>
    public Guid RightId { get; init; }
}