using System.ComponentModel.DataAnnotations;

namespace Application.UseCases.Risks.Dto;

/// <summary>Данные RiskPassportRequest в клиентской базе</summary>
public sealed record RiskPassportRequest
{
    /// <summary>Название</summary>
    [Required, StringLength(250)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Описание</summary>
    [Required, StringLength(16000)]
    public string Description { get; init; } = string.Empty;

    /// <summary>Опасность</summary>
    public Guid HazardId { get; init; }

    /// <summary>Категория</summary>
    public Guid CategoryId { get; init; }

    /// <summary>Объект</summary>
    public Guid? AssetId { get; init; }

    /// <summary>Основной процесс при создании</summary>
    public Guid? ProcessId { get; init; }

    /// <summary>Ожидаемая версия риска</summary>
    public Guid? Version { get; init; }
}
