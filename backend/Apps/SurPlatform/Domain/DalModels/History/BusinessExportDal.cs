using Domain.DalModels.Base;

namespace Domain.DalModels.History;

/// <summary>
/// Запрос выгрузки согласованного снимка административной истории
/// </summary>
public sealed record BusinessExportDal : DalModelBase
{
    /// <summary>
    /// Первая включенная дата периода истории по UTC
    /// </summary>
    public DateTime From { get; init; }

    /// <summary>
    /// Последняя включенная дата периода истории по UTC
    /// </summary>
    public DateTime To { get; init; }

    /// <summary>
    /// Идентификатор администратора, запросившего выгрузку
    /// </summary>
    public Guid ActorId { get; init; }

    /// <summary>
    /// Время регистрации выгрузки в UTC
    /// </summary>
    public DateTime CreatedAt { get; init; }
}