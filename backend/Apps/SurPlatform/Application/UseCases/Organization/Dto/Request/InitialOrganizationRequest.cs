using System.ComponentModel.DataAnnotations;

namespace Application.UseCases.Organization.Dto.Request;

/// <summary>
/// Параметры административной операции оргструктуры
/// </summary>
public sealed record InitialOrganizationRequest
{
    /// <summary>
    /// Основная дирекция сотрудника
    /// </summary>
    public Guid? DirectorateId { get; init; }

    /// <summary>
    /// Должность
    /// </summary>
    [StringLength(250)]
    public string? PositionName { get; init; }

    /// <summary>
    /// Начальные назначения
    /// </summary>
    [Required, MaxLength(100)]
    public InitialResponsibilityRequest[] Responsibilities { get; init; } = [];
}
