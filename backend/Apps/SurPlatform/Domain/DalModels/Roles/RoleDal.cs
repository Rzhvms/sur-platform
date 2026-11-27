using Domain.DalModels.Base;

namespace Domain.DalModels.Roles;

/// <summary>
/// Роль из каталога, объединяющая назначения прав
/// </summary>
public sealed record RoleDal : VersionedDalModelBase
{
    /// <summary>
    /// Отображаемое название роли
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Признак роли, поставляемой системным каталогом
    /// </summary>
    public bool IsSystem { get; init; }

    /// <summary>
    /// Стабильный код роли для прикладных проверок
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// Описание назначения роли
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Признак роли, участвующей в вычислении эффективных прав
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Время создания роли в UTC
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Время последнего изменения роли в UTC
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}