namespace Domain.DalModels;

/// <summary>Факт атомарного первоначального заполнения демонстрационного набора</summary>
public sealed record DemoDataSeedDal
{
    /// <summary>Номер демонстрационного набора</summary>
    public int Id { get; init; }

    /// <summary>Момент завершения заполнения UTC</summary>
    public DateTime AppliedAt { get; init; }
}
