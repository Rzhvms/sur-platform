namespace Application.Models;

/// <summary>
/// Системная роль и её текущие права
/// </summary>
public sealed record IdentityRole
{
    /// <summary>
    /// Идентификатор роли
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Системный код
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// Отображаемое название
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Назначение роли
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Активность роли
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Версия матрицы
    /// </summary>
    public Guid Version { get; init; }

    /// <summary>
    /// Время создания
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Время изменения
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Назначенные права
    /// </summary>
    public Guid[] RightIds { get; init; } = [];
}
