namespace Domain.DalModels.Base;

/// <summary>
/// Основа версионируемой записи, связанной с учетной записью пользователя
/// </summary>
public abstract record BaseUserModel : VersionedDalModelBase
{
    /// <summary>
    /// Идентификатор пользователя, к которому относится запись
    /// </summary>
    public Guid UserId { get; init; }
}