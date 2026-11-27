namespace Domain.DalModels;

/// <summary>Данные BusinessExportDal в клиентской базе</summary>
public sealed record BusinessExportDal
{
    /// <summary>Запрос выгрузки</summary>
    public Guid Id { get; init; }

    /// <summary>Первая дата UTC</summary>
    public DateTime From { get; init; }

    /// <summary>Последняя дата UTC</summary>
    public DateTime To { get; init; }

    /// <summary>Администратор</summary>
    public Guid ActorId { get; init; }

    /// <summary>Момент формирования UTC</summary>
    public DateTime CreatedAt { get; init; }
}
