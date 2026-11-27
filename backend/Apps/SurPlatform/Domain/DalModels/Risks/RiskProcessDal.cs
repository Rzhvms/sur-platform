using Domain.DalModels.Base;

namespace Domain.DalModels.Risks;

/// <summary>
/// Связь риска с процессом, сохраняющая владельца и период действия
/// </summary>
public sealed record RiskProcessDal : VersionedDalModelBase
{
    /// <summary>
    /// Идентификатор связанного риска
    /// </summary>
    public Guid RiskId { get; init; }

    /// <summary>
    /// Идентификатор процесса, к которому относится риск
    /// </summary>
    public Guid ProcessId { get; init; }

    /// <summary>
    /// Идентификатор сотрудника, являющегося владельцем процесса в этой связи
    /// </summary>
    public Guid OwnerId { get; init; }

    /// <summary>
    /// Признак основного процесса риска
    /// </summary>
    public bool IsPrimary { get; init; }

    /// <summary>
    /// Время начала действия связи в UTC
    /// </summary>
    public DateTime LinkedAt { get; init; }

    /// <summary>
    /// Время окончания действия связи в UTC, если она закрыта
    /// </summary>
    public DateTime? UnlinkedAt { get; init; }
}