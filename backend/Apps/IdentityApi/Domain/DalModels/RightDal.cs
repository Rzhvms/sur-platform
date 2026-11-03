namespace Domain.DalModels;

/// <summary>
/// Модель прав пользователя
/// </summary>
public sealed record RightDal
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
    /// Признак системного права
    /// </summary>
    public bool IsSystem { get; init; }
}
