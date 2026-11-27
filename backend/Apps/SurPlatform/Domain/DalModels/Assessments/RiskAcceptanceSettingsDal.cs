using Domain.DalModels.Base;

namespace Domain.DalModels.Assessments;

/// <summary>
/// Пороговые баллы для определения уровня принятия риска
/// </summary>
public sealed record RiskAcceptanceSettingsDal : VersionedDalModelBase
{
    /// <summary>
    /// Максимальный балл риска, доступный для принятия владельцу процесса
    /// </summary>
    public int OwnerMaxScore { get; init; }

    /// <summary>
    /// Максимальный балл риска, доступный для принятия директору
    /// </summary>
    public int DirectorMaxScore { get; init; }

    /// <summary>
    /// Максимальный балл риска, доступный для принятия генеральному директору
    /// </summary>
    public int CeoMaxScore { get; init; }

    /// <summary>
    /// Идентификатор пользователя, последним изменившего пороги
    /// </summary>
    public Guid? UpdatedById { get; init; }

    /// <summary>
    /// Время последнего изменения порогов в UTC
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}