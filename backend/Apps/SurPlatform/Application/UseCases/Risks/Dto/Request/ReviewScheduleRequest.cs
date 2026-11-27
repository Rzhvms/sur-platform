namespace Application.UseCases.Risks.Dto.Request;

/// <summary>Поля административной команды пересмотра</summary>
public sealed record ReviewScheduleRequest
{
    /// <summary>Ожидаемая версия</summary>
    public Guid? Version { get; init; }

    /// <summary>Название расписания</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Активный ответственный</summary>
    public Guid ResponsibleUserId { get; init; }

    /// <summary>Разрешены ли новые срабатывания</summary>
    public bool IsActive { get; init; } = true;

    /// <summary>Первая допустимая дата</summary>
    public DateOnly StartDate { get; init; }

    /// <summary>Тип повторения</summary>
    public string RecurrenceType { get; init; } = string.Empty;

    /// <summary>Интервал повторения</summary>
    public int? RecurrenceInterval { get; init; }

    /// <summary>Дни недели ISO от 1 до 7</summary>
    public int[]? Weekdays { get; init; }

    /// <summary>Условие окончания</summary>
    public string EndType { get; init; } = string.Empty;

    /// <summary>Число срабатываний</summary>
    public int? OccurrenceCount { get; init; }

    /// <summary>Включительная дата окончания</summary>
    public DateOnly? EndDate { get; init; }
}
