namespace Application.UseCases.Risks.Dto.Request;

/// <summary>Публикация денежных границ</summary>
public sealed record ImpactScaleRequest
{
    /// <summary>Текущая версия, null для первой публикации</summary>
    public Guid? Version { get; init; }

    /// <summary>Пять включительных нижних порогов уровней 1–5</summary>
    public decimal[]? Minimums { get; init; }
}
