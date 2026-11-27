namespace Domain.DalModels.Organization;

/// <summary>
/// Общие данные идентификации, активности и версии записи оргструктуры
/// </summary>
public interface IOrganizationRow
{
    /// <summary>
    /// Идентификатор записи оргструктуры
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Версия записи для проверки конкурентного изменения
    /// </summary>
    Guid Version { get; }

    /// <summary>
    /// Доступность записи для новых организационных связей
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Время последнего изменения записи в UTC
    /// </summary>
    DateTime UpdatedAt { get; }
}