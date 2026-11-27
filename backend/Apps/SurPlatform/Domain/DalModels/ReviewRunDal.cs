using System.Text.Json.Serialization;

namespace Domain.DalModels;

/// <summary>Неизменяемый источник и итог первичной обработки</summary>
public sealed record ReviewRunDal
{
    /// <summary>Запуск</summary>
    public Guid Id { get; init; }

    /// <summary>Основание</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>Тип источника</summary>
    public string SourceType { get; init; } = string.Empty;

    /// <summary>Идентификатор источника</summary>
    public Guid SourceId { get; init; }

    /// <summary>Название настройки на момент запуска</summary>
    public string? SourceNameSnapshot { get; init; }

    /// <summary>Название внешней системы на момент запуска</summary>
    public string? ExternalSystemNameSnapshot { get; init; }

    /// <summary>Инициатор</summary>
    public Guid InitiatedById { get; init; }

    /// <summary>Администратор, изменивший настройку</summary>
    public Guid? ConfigurationAuthorId { get; init; }

    /// <summary>Ответственный за запуск</summary>
    public Guid? ResponsibleUserId { get; init; }

    /// <summary>Календарная зона запуска</summary>
    public string? TimeZone { get; init; }

    /// <summary>Плановая календарная дата</summary>
    public DateTime? ScheduledDate { get; init; }

    /// <summary>Момент регистрации UTC</summary>
    public DateTime OccurredAt { get; init; }

    /// <summary>Итог первичной обработки</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Исходные байты принятого тела</summary>
    [JsonIgnore]
    public byte[]? RawPayload { get; init; }

    /// <summary>Снимок целей для возобновления обработки</summary>
    [JsonIgnore]
    public Guid[] TargetRiskIds { get; init; } = [];
}
