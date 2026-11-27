namespace Application.Models;

/// <summary>
/// Право из системного каталога
/// </summary>
public sealed record IdentityRight
{
    /// <summary>
    /// Идентификатор права
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Системный код права
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// Отображаемое название
    /// </summary>
    public string? Name { get; init; }
}
