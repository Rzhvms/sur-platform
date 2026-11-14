namespace Domain.DalModels;

/// <summary>Пороги маршрута встроенного метода</summary>
public sealed record RiskAcceptanceSettingsDal
{
    /// <summary>Идентификатор настройки</summary>
    public Guid Id { get; init; }

    /// <summary>Версия для конкурентной записи</summary>
    public Guid Version { get; init; }

    /// <summary>Верхняя граница владельца</summary>
    public int OwnerMaxScore { get; init; }

    /// <summary>Верхняя граница директора</summary>
    public int DirectorMaxScore { get; init; }

    /// <summary>Верхняя граница CEO</summary>
    public int CeoMaxScore { get; init; }

    /// <summary>Автор последнего изменения</summary>
    public Guid? UpdatedById { get; init; }

    /// <summary>Момент последнего изменения</summary>
    public DateTime UpdatedAt { get; init; }
}
