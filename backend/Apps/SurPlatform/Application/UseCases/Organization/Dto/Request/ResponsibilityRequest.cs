using System.ComponentModel.DataAnnotations;

namespace Application.UseCases.Organization.Dto.Request;

/// <summary>
/// Параметры административной операции оргструктуры
/// </summary>
public sealed record ResponsibilityRequest
{
    /// <summary>
    /// Код CEO, DIRECTOR или RISK_SPECIALIST
    /// </summary>
    [Required]
    public string ResponsibilityType { get; init; } = string.Empty;

    /// <summary>
    /// Дирекция ответственности
    /// </summary>
    public Guid? DirectorateId { get; init; }

    /// <summary>
    /// Выбранная запись сотрудника
    /// </summary>
    public Guid? PersonId { get; init; }

    /// <summary>
    /// Пользователь для назначения CEO
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Должность при создании сотрудника CEO
    /// </summary>
    [StringLength(250)]
    public string? PositionName { get; init; }

    /// <summary>
    /// Заменяемое действующее назначение
    /// </summary>
    public Guid? PreviousId { get; init; }

    /// <summary>
    /// Ожидаемая версия заменяемого назначения
    /// </summary>
    public Guid? PreviousVersion { get; init; }
}
