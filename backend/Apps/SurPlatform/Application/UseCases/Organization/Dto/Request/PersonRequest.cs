using System.ComponentModel.DataAnnotations;

namespace Application.UseCases.Organization.Dto.Request;

/// <summary>
/// Параметры административной операции оргструктуры
/// </summary>
public sealed record PersonRequest
{
    /// <summary>
    /// Учётная запись
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Основная дирекция
    /// </summary>
    public Guid DirectorateId { get; init; }

    /// <summary>
    /// Должность
    /// </summary>
    [StringLength(250)]
    public string? PositionName { get; init; }
}
