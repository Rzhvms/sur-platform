using System.Text.Json.Serialization;
using Domain.DalModels.Base;

namespace Domain.DalModels.Reviews;

/// <summary>
/// Настройка доверенного внешнего источника пересмотра рисков
/// </summary>
public sealed record ReviewWebhookDal : VersionedDalModelBase
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
    /// Название внешней системы, отправляющей сигналы пересмотра
    /// </summary>
    public string ExternalSystemName { get; init; } = string.Empty;

    /// <summary>
    /// Хеш Bearer-секрета источника, исключенный из JSON-ответов
    /// </summary>
    [JsonIgnore]
    public byte[] TokenHash { get; init; } = [];
}