namespace Application.Settings;

/// <summary>
/// Настройки календарной зоны новых расписаний пересмотра
/// </summary>
public sealed class ReviewCalendarSettings
{
    /// <summary>
    /// Ключ календарной зоны в конфигурации приложения
    /// </summary>
    public const string TimeZoneKey = "Application:TimeZone";

    /// <summary>
    /// Календарная зона при отсутствии настройки
    /// </summary>
    public const string DefaultTimeZone = "UTC";

    /// <summary>
    /// Календарная зона, используемая при создании расписаний
    /// </summary>
    public string TimeZone { get; init; } = DefaultTimeZone;
}