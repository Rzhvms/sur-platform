using System.ComponentModel.DataAnnotations;

namespace Application.UseCases.Organization.Dto.Request;

/// <summary>
/// Параметры административной операции оргструктуры
/// </summary>
public sealed record PersonEditRequest
{
    /// <summary>
    /// Версия сотрудника
    /// </summary>
    public Guid Version { get; init; }

    /// <summary>
    /// Новая должность
    /// </summary>
    [StringLength(250)]
    public string? PositionName { get; init; }
}
