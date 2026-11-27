using System.ComponentModel.DataAnnotations;

namespace Application.UseCases.Organization.Dto.Request;

/// <summary>
/// Параметры административной операции оргструктуры
/// </summary>
public sealed record DirectorateRequest
{
    /// <summary>
    /// Название дирекции
    /// </summary>
    [Required, StringLength(250)]
    public string Name { get; init; } = string.Empty;
}
