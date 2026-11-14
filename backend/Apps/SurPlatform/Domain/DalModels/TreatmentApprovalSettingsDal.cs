namespace Domain.DalModels;

/// <summary>Хранение данных этапа выполнения мер</summary>
public sealed record TreatmentApprovalSettingsDal
{
    /// <summary>Идентификатор записи</summary>
    public Guid Id { get; init; }

    /// <summary>Версия для конкурентной записи</summary>
    public Guid Version { get; init; }

    /// <summary>Нижний порог владельца</summary>
    public decimal OwnerThreshold { get; init; }

    /// <summary>Нижний порог директора</summary>
    public decimal DirectorThreshold { get; init; }

    /// <summary>Нижний порог CEO</summary>
    public decimal CeoThreshold { get; init; }
}
