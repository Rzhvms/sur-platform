using System.Text.Json.Serialization;
using Domain.DalModels.Base;

namespace Domain.DalModels.Reviews;

/// <summary>
/// Сохраненный запуск пересмотра с источником, целями и итогом первичной обработки
/// </summary>
public sealed record ReviewRunDal : DalModelBase
{
    /// <summary>
    /// Код основания запуска пересмотра
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Код типа источника, вызвавшего пересмотр
    /// </summary>
    public string SourceType { get; init; } = string.Empty;

    /// <summary>
    /// Идентификатор источника запуска пересмотра
    /// </summary>
    public Guid SourceId { get; init; }

    /// <summary>
    /// Название настройки источника на момент запуска
    /// </summary>
    public string? SourceNameSnapshot { get; init; }

    /// <summary>
    /// Название внешней системы на момент запуска
    /// </summary>
    public string? ExternalSystemNameSnapshot { get; init; }

    /// <summary>
    /// Идентификатор пользователя или системного субъекта, инициировавшего запуск
    /// </summary>
    public Guid InitiatedById { get; init; }

    /// <summary>
    /// Идентификатор администратора, изменившего использованную настройку
    /// </summary>
    public Guid? ConfigurationAuthorId { get; init; }

    /// <summary>
    /// Идентификатор пользователя, ответственного за запуск
    /// </summary>
    public Guid? ResponsibleUserId { get; init; }

    /// <summary>
    /// Календарная зона, зафиксированная для запуска
    /// </summary>
    public string? TimeZone { get; init; }

    /// <summary>
    /// Плановая календарная дата запуска без преобразования в UTC
    /// </summary>
    public DateTime? ScheduledDate { get; init; }

    /// <summary>
    /// Время регистрации запуска в UTC
    /// </summary>
    public DateTime OccurredAt { get; init; }

    /// <summary>
    /// Код результата первичной обработки запуска
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Исходные байты принятого webhook, исключенные из JSON-ответов
    /// </summary>
    [JsonIgnore]
    public byte[]? RawPayload { get; init; }

    /// <summary>
    /// Зафиксированные идентификаторы целевых рисков для возобновления обработки, исключенные из JSON-ответов
    /// </summary>
    [JsonIgnore]
    public Guid[] TargetRiskIds { get; init; } = [];
}