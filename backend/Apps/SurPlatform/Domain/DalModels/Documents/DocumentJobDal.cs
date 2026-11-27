using Domain.DalModels.Base;

namespace Domain.DalModels.Documents;

/// <summary>
/// Сохраненное задание формирования документа или импорта CSV
/// </summary>
public sealed record DocumentJobDal : DalModelBase
{
    /// <summary>
    /// Ключ идемпотентности регистрации задания
    /// </summary>
    public Guid OperationId { get; init; }

    /// <summary>
    /// Идентификатор пользователя, которому принадлежит задание
    /// </summary>
    public Guid OwnerId { get; init; }

    /// <summary>
    /// Идентификатор сессии, в которой зарегистрировано задание
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// Версия доступа пользователя, зафиксированная при регистрации задания
    /// </summary>
    public long AccessVersion { get; init; }

    /// <summary>
    /// Вид задания: отчет, история изменений или импорт
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Текущее состояние обработки задания
    /// </summary>
    public string State { get; init; } = string.Empty;

    /// <summary>
    /// Зафиксированные параметры задания в JSON
    /// </summary>
    public string Parameters { get; init; } = string.Empty;

    /// <summary>
    /// Сопоставление колонок CSV с полями импорта в JSON
    /// </summary>
    public string? Mapping { get; init; }

    /// <summary>
    /// Размер исходного или сформированного файла в байтах
    /// </summary>
    public long? Size { get; init; }

    /// <summary>
    /// Хеш SHA-256 исходного или сформированного файла в шестнадцатеричном формате
    /// </summary>
    public string? Checksum { get; init; }

    /// <summary>
    /// Время регистрации задания в UTC
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Время фиксации согласованного снимка данных в UTC
    /// </summary>
    public DateTime? CapturedAt { get; init; }

    /// <summary>
    /// Время формирования доступного для скачивания файла в UTC
    /// </summary>
    public DateTime? GeneratedAt { get; init; }

    /// <summary>
    /// Время завершения обработки задания в UTC
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Стабильный код ошибки обработки без содержимого файлов
    /// </summary>
    public string? Error { get; init; }
}