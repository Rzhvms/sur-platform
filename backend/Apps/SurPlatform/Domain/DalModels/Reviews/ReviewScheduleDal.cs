using Domain.DalModels.Base;

namespace Domain.DalModels.Reviews;

/// <summary>
/// Календарное расписание автоматического пересмотра рисков
/// </summary>
public sealed record ReviewScheduleDal : VersionedDalModelBase
{
    /// <summary>
    /// Название источника пересмотра
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Разрешение новых запусков из этого источника
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Идентификатор пользователя, ответственного за источник
    /// </summary>
    public Guid ResponsibleUserId { get; init; }

    /// <summary>
    /// Идентификатор администратора, создавшего настройку
    /// </summary>
    public Guid CreatedById { get; init; }

    /// <summary>
    /// Идентификатор администратора, последним изменившего настройку
    /// </summary>
    public Guid UpdatedById { get; init; }

    /// <summary>
    /// Время создания настройки в UTC
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Время последнего изменения настройки в UTC
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// Сохраненная календарная зона расчета дат запуска
    /// </summary>
    public string TimeZone { get; init; } = string.Empty;

    /// <summary>
    /// Календарная дата начала расписания без преобразования в UTC
    /// </summary>
    public DateTime StartDate { get; init; }

    /// <summary>
    /// Код периодичности запусков
    /// </summary>
    public string RecurrenceType { get; init; } = string.Empty;

    /// <summary>
    /// Интервал повторения в единицах выбранной периодичности
    /// </summary>
    public int? RecurrenceInterval { get; init; }

    /// <summary>
    /// Номера дней недели по ISO для выбранного расписания
    /// </summary>
    public int[] Weekdays { get; init; } = [];

    /// <summary>
    /// Код условия окончания расписания
    /// </summary>
    public string EndType { get; init; } = string.Empty;

    /// <summary>
    /// Предельное число запусков, если окончание задано по количеству
    /// </summary>
    public int? OccurrenceCount { get; init; }

    /// <summary>
    /// Последняя допустимая календарная дата запуска, если окончание задано по дате
    /// </summary>
    public DateTime? EndDate { get; init; }
}