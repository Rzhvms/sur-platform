namespace Domain.DalModels;

/// <summary>
/// Строка оргструктуры клиента
/// </summary>
public sealed record OrgPersonDal : IOrganizationRow
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
    /// Связанная учётная запись
    /// </summary>
    public Guid UserId { get; init; }
    /// <summary>
    /// Основная дирекция
    /// </summary>
    public Guid? DirectorateId { get; init; }
    /// <summary>
    /// Должность
    /// </summary>
    public string? PositionName { get; init; }
    /// <summary>
    /// ФИО из связанной учётной записи
    /// </summary>
    public string? FullName { get; init; }
    /// <summary>
    /// Почта из связанной учётной записи
    /// </summary>
    public string? Email { get; init; }
}
