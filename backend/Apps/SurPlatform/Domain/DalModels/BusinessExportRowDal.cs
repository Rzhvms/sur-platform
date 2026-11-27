namespace Domain.DalModels;

/// <summary>Данные BusinessExportRowDal в клиентской базе</summary>
public sealed record BusinessExportRowDal
{
    /// <summary>Запрос выгрузки</summary>
    public Guid ExportId { get; init; }

    /// <summary>Событие снимка</summary>
    public Guid EventId { get; init; }

    /// <summary>Зафиксированное событие</summary>
    public string Payload { get; init; } = string.Empty;
}
