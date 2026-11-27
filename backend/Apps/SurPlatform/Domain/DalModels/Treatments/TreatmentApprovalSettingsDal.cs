using Domain.DalModels.Base;

namespace Domain.DalModels.Treatments;

/// <summary>
/// Денежные пороги определения уровня согласования мер
/// </summary>
public sealed record TreatmentApprovalSettingsDal : VersionedDalModelBase
{
    /// <summary>
    /// Нижний порог плановых расходов в рублях для уровня владельца процесса
    /// </summary>
    public decimal OwnerThreshold { get; init; }

    /// <summary>
    /// Нижний порог плановых расходов в рублях для уровня директора
    /// </summary>
    public decimal DirectorThreshold { get; init; }

    /// <summary>
    /// Нижний порог плановых расходов в рублях для уровня генерального директора
    /// </summary>
    public decimal CeoThreshold { get; init; }
}