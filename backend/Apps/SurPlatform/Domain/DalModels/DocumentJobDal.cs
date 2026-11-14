namespace Domain.DalModels;

/// <summary>Долговременное состояние файла и его прикладного задания</summary>
public sealed record DocumentJobDal
{
    /// <summary>Серверный идентификатор задания и файла</summary>
    public Guid Id { get; init; }

    /// <summary>Ключ повторяемой команды</summary>
    public Guid OperationId { get; init; }

    /// <summary>Владелец задания</summary>
    public Guid OwnerId { get; init; }

    /// <summary>Сеанс, разрешивший обработку</summary>
    public Guid SessionId { get; init; }

    /// <summary>Подтверждённая версия прав</summary>
    public long AccessVersion { get; init; }

    /// <summary>Назначение задания</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Подтверждённый этап обработки</summary>
    public string State { get; init; } = string.Empty;

    /// <summary>Зафиксированные параметры в JSON</summary>
    public string Parameters { get; init; } = string.Empty;

    /// <summary>Зафиксированное сопоставление импорта</summary>
    public string? Mapping { get; init; }

    /// <summary>Размер подтверждаемого объекта в байтах</summary>
    public long? Size { get; init; }

    /// <summary>Контрольная сумма объекта SHA-256 в hex</summary>
    public string? Checksum { get; init; }

    /// <summary>Время регистрации</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Время фиксации согласованного снимка</summary>
    public DateTime? CapturedAt { get; init; }

    /// <summary>Время формирования опубликованного файла</summary>
    public DateTime? GeneratedAt { get; init; }

    /// <summary>Время завершения обработки</summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>Стабильный код сбоя задания</summary>
    public string? Error { get; init; }
}
