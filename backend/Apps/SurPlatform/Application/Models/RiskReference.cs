namespace Application.Models;

/// <summary>Данные RiskReference в клиентской базе</summary>
public sealed record RiskReference
{
    /// <summary>Идентификатор справочной записи</summary>
    public Guid Id { get; init; }

    /// <summary>Версия записи</summary>
    public Guid Version { get; init; }

    /// <summary>Название</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Описание</summary>
    public string? Description { get; init; }

    /// <summary>Текущее состояние записи</summary>
    public bool IsActive { get; init; }

    /// <summary>Группа опасности</summary>
    public Guid? GroupId { get; init; }
}
