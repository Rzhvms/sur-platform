namespace Domain.DalModels;

/// <summary>
/// Общие поля версионированной записи оргструктуры
/// </summary>
public interface IOrganizationRow
{
    /// <summary>Идентификатор записи</summary>
    Guid Id { get; }

    /// <summary>Версия изменения</summary>
    Guid Version { get; }

    /// <summary>Доступность записи для новых связей</summary>
    bool IsActive { get; }

    /// <summary>Момент последнего изменения</summary>
    DateTime UpdatedAt { get; }
}
