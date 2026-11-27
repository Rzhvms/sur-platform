using System.ComponentModel.DataAnnotations;

namespace Application.UseCases.Risks.Dto;

/// <summary>Данные RiskListQuery в клиентской базе</summary>
public sealed record RiskListQuery
{
    /// <summary>Поиск по названию</summary>
    [StringLength(250)]
    public string? Search { get; init; }

    /// <summary>Дирекция</summary>
    public Guid? DirectorateId { get; init; }

    /// <summary>Процесс</summary>
    public Guid? ProcessId { get; init; }

    /// <summary>Сотрудник-владелец</summary>
    public Guid? OwnerId { get; init; }

    /// <summary>Объект</summary>
    public Guid? AssetId { get; init; }

    /// <summary>Категория</summary>
    public Guid? CategoryId { get; init; }

    /// <summary>Статус</summary>
    public string? Status { get; init; }

    /// <summary>Сортировка</summary>
    public string? Sort { get; init; }

    /// <summary>Смещение</summary>
    [Range(0, int.MaxValue)]
    public int Offset { get; init; }

    /// <summary>Размер страницы</summary>
    [Range(1, 100)]
    public int Limit { get; init; } = 25;
}
