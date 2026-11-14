namespace Domain.DalModels;

/// <summary>
/// Строка оргструктуры клиента
/// </summary>
public sealed record ProcessDal : IOrganizationRow
{
    /// <summary>
    /// Идентификатор записи
    /// </summary>
    public Guid Id { get; init; }
    /// <summary>
    /// Версия для проверки конкурентного изменения
    /// </summary>
    public Guid Version { get; init; }
    /// <summary>
    /// Доступность для новых связей
    /// </summary>
    public bool IsActive { get; init; }
    /// <summary>
    /// Время создания UTC
    /// </summary>
    public DateTime CreatedAt { get; init; }
    /// <summary>
    /// Время последнего изменения UTC
    /// </summary>
    public DateTime UpdatedAt { get; init; }
    /// <summary>
    /// Дирекция процесса
    /// </summary>
    public Guid DirectorateId { get; init; }
    /// <summary>
    /// Действующий владелец процесса
    /// </summary>
    public Guid OwnerId { get; init; }
    /// <summary>
    /// Название процесса
    /// </summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>
    /// Описание процесса
    /// </summary>
    public string? Description { get; init; }
}
