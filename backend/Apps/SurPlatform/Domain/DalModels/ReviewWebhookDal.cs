using System.Text.Json.Serialization;

namespace Domain.DalModels;

/// <summary>Настройка внешнего запуска пересмотра</summary>
public sealed record ReviewWebhookDal
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

    /// <summary>Название доверенной внешней системы</summary>
    public string ExternalSystemName { get; init; } = string.Empty;

    /// <summary>Хеш случайного Bearer-секрета</summary>
    [JsonIgnore]
    public byte[] TokenHash { get; init; } = [];
}
