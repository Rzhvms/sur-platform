using System.ComponentModel.DataAnnotations;

namespace Application.UseCases.Organization.Dto.Request;

/// <summary>
/// Параметры административной операции оргструктуры
/// </summary>
public sealed record AssetRequest
{
    /// <summary>
    /// Название объекта
    /// </summary>
    [Required, StringLength(250)]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Описание
    /// </summary>
    [StringLength(4000)]
    public string? Description { get; init; }

    /// <summary>
    /// Инвентарный номер
    /// </summary>
    [StringLength(250)]
    public string? InventoryNumber { get; init; }

    /// <summary>
    /// Стоимость простоя в рублях
    /// </summary>
    [Required]
    public decimal? HourlyDowntimeCost { get; init; }
}
