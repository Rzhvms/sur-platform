namespace Domain.DalModels.History;

/// <summary>
/// Событие журнала, зафиксированное в снимке административной выгрузки
/// </summary>
public sealed record BusinessExportRowDal
{
    /// <summary>
    /// Идентификатор выгрузки, которой принадлежит событие
    /// </summary>
    public Guid ExportId { get; init; }

    /// <summary>
    /// Идентификатор события исходного журнала
    /// </summary>
    public Guid EventId { get; init; }

    /// <summary>
    /// Содержимое события в JSON на момент фиксации снимка
    /// </summary>
    public string Payload { get; init; } = string.Empty;
}