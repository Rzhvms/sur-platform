namespace Application.UseCases.Treatments.Dto.Request;

/// <summary>Поля команды работы с мерой</summary>
public sealed record TreatmentThresholdsRequest
{
    /// <summary>Версия меры или запроса</summary>
    public Guid Version { get; init; }

    /// <summary>Нижний порог владельца</summary>
    public decimal? OwnerThreshold { get; init; }

    /// <summary>Нижний порог директора</summary>
    public decimal? DirectorThreshold { get; init; }

    /// <summary>Нижний порог CEO</summary>
    public decimal? CeoThreshold { get; init; }
}
