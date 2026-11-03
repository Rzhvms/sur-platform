namespace Domain.DalModels;

/// <summary>
/// Модель роли пользователя
/// </summary>
public sealed record RoleDal
{
    /// <summary>
    /// Идентификатор записи
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Отображаемое название
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Признак системной роли
    /// </summary>
    public bool IsSystem { get; init; }
}