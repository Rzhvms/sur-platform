namespace Domain.DalModels;

/// <summary>Календарная настройка пересмотра</summary>
public sealed record ReviewScheduleDal
{
    /// <summary>Идентификатор настройки</summary>
    public Guid Id { get; init; }

    /// <summary>Версия административной формы</summary>
    public Guid Version { get; init; }

    /// <summary>Название источника</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Разрешены ли новые запуски</summary>
    public bool IsActive { get; init; }

    /// <summary>Ответственный за источник</summary>
    public Guid ResponsibleUserId { get; init; }

    /// <summary>Кто создал настройку</summary>
    public Guid CreatedById { get; init; }

    /// <summary>Кто последним изменил настройку</summary>
    public Guid UpdatedById { get; init; }

    /// <summary>Время создания UTC</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Время последнего изменения UTC</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>Сохранённая календарная зона</summary>
    public string TimeZone { get; init; } = string.Empty;

    /// <summary>Календарная дата начала</summary>
    public DateTime StartDate { get; init; }

    /// <summary>Периодичность</summary>
    public string RecurrenceType { get; init; } = string.Empty;

    /// <summary>Интервал периодичности</summary>
    public int? RecurrenceInterval { get; init; }

    /// <summary>Дни недели в формате ISO</summary>
    public int[] Weekdays { get; init; } = [];

    /// <summary>Условие окончания</summary>
    public string EndType { get; init; } = string.Empty;

    /// <summary>Предельное число срабатываний</summary>
    public int? OccurrenceCount { get; init; }

    /// <summary>Последняя допустимая дата</summary>
    public DateTime? EndDate { get; init; }
}
