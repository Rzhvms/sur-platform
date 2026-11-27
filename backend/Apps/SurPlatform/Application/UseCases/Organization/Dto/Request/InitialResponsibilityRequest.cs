using System.ComponentModel.DataAnnotations;

namespace Application.UseCases.Organization.Dto.Request;

/// <summary>
/// Параметры административной операции оргструктуры
/// </summary>
public sealed record InitialResponsibilityRequest
{
    /// <summary>
    /// Код организационной ответственности
    /// </summary>
    [Required]
    public string ResponsibilityType { get; init; } = string.Empty;

    /// <summary>
    /// Дирекция ответственности
    /// </summary>
    public Guid? DirectorateId { get; init; }

    /// <summary>
    /// Момент проверки свободной позиции, полученный от API
    /// </summary>
    public DateTimeOffset ObservedAt { get; init; }
}
