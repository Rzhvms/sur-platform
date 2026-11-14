namespace Domain.DalModels;

/// <summary>Данные HazardDal в клиентской базе</summary>
public sealed record HazardDal
{
    /// <summary>Идентификатор записи</summary>
    public Guid Id { get; init; }

    /// <summary>Ожидаемая версия при изменении</summary>
    public Guid Version { get; init; }

    /// <summary>Название</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Описание</summary>
    public string? Description { get; init; }

    /// <summary>Доступность для новых связей</summary>
    public bool IsActive { get; init; }

    /// <summary>Группа опасности</summary>
    public Guid GroupId { get; init; }
}
