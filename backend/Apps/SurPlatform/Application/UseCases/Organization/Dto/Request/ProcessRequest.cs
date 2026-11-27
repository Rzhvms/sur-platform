using System.ComponentModel.DataAnnotations;

namespace Application.UseCases.Organization.Dto.Request;

/// <summary>
/// Параметры административной операции оргструктуры
/// </summary>
public sealed record ProcessRequest
{
    /// <summary>
    /// Дирекция
    /// </summary>
    public Guid DirectorateId { get; init; }

    /// <summary>
    /// Сотрудник с ролью OWNER
    /// </summary>
    public Guid OwnerId { get; init; }

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
