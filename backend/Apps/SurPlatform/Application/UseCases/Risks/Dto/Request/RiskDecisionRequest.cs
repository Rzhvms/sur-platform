namespace Application.UseCases.Risks.Dto.Request;

/// <summary>Выбор способа работы с риском</summary>
public sealed record RiskDecisionRequest
{
    /// <summary>Версия риска</summary>
    public Guid Version { get; init; }

    /// <summary>Полный набор процессов при разделении</summary>
    public Guid[]? ProcessIds { get; init; }

    /// <summary>Основной процесс при разделении</summary>
    public Guid? PrimaryProcessId { get; init; }
}
