namespace Domain.DalModels.Demo;

/// <summary>
/// Отметка о завершенном заполнении демонстрационного набора
/// </summary>
public sealed record DemoDataSeedDal
{
    /// <summary>
    /// Номер примененного демонстрационного набора
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Время завершения заполнения в UTC
    /// </summary>
    public DateTime AppliedAt { get; init; }
}