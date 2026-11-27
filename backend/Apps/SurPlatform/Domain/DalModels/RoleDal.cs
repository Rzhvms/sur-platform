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

    /// <summary>
    /// Системный код из каталога
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// Назначение системной роли
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Признак действующей записи
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Версия для контроля конкурентных изменений
    /// </summary>
    public Guid Version { get; init; }

    /// <summary>
    /// Момент создания записи
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Момент изменения записи
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}
