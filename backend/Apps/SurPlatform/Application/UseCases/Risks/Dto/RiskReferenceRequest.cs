using System.ComponentModel.DataAnnotations;

namespace Application.UseCases.Risks.Dto;

/// <summary>Данные RiskReferenceRequest в клиентской базе</summary>
public sealed record RiskReferenceRequest
{
    /// <summary>Название</summary>
    [Required, StringLength(250)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Описание</summary>
    [StringLength(4000)]
    public string? Description { get; init; }

    /// <summary>Группа для опасности</summary>
    public Guid? GroupId { get; init; }

    /// <summary>Версия для редактирования</summary>
    public Guid? Version { get; init; }

    /// <summary>Новое состояние</summary>
    public bool? IsActive { get; init; }
}
