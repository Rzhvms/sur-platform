namespace Application.UseCases.Risks.Dto.Request;

/// <summary>Передача риска в один новый процесс</summary>
public sealed record TransferRiskRequest
{
    /// <summary>Ожидаемая версия риска</summary>
    public Guid Version { get; init; }

    /// <summary>Единственный целевой процесс</summary>
    public Guid ProcessId { get; init; }
}
