using Domain.DalModels.Base;

namespace Domain.DalModels.Rights;

/// <summary>
/// Право из каталога доступных операций системы
/// </summary>
public sealed record RightDal : DalModelBase
{
    /// <summary>
    /// Отображаемое название права
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Признак права, поставляемого системным каталогом
    /// </summary>
    public bool IsSystem { get; init; }

    /// <summary>
    /// Стабильный код права для проверок доступа
    /// </summary>
    public string? Code { get; init; }
}