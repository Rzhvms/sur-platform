using System.ComponentModel.DataAnnotations;

namespace Application.UseCases.Organization.Dto.Request;

/// <summary>
/// Параметры административной операции оргструктуры
/// </summary>
public sealed record ProcessEditRequest
{
    /// <summary>
    /// Версия процесса
    /// </summary>
    public Guid Version { get; init; }

    /// <summary>
    /// Название процесса
    /// </summary>
    [Required, StringLength(250)]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Описание процесса
    /// </summary>
    [StringLength(4000)]
    public string? Description { get; init; }
}
