namespace Application.UseCases.Risks.Dto;

/// <summary>Данные BusinessPeriod в клиентской базе</summary>
public sealed record BusinessPeriod
{
    /// <summary>Первая дата UTC включительно</summary>
    public DateOnly From { get; init; }

    /// <summary>Последняя дата UTC включительно</summary>
    public DateOnly To { get; init; }
}
