using Domain.DalModels.Base;

namespace Domain.DalModels.Treatments;

/// <summary>
/// Сохраненное назначение сотрудника исполнителем меры
/// </summary>
public sealed record TreatmentAssignmentDal : DalModelBase
{
    /// <summary>
    /// Идентификатор меры, для которой назначен исполнитель
    /// </summary>
    public Guid TreatmentId { get; init; }

    /// <summary>
    /// Идентификатор сотрудника, назначенного исполнителем
    /// </summary>
    public Guid AssigneeId { get; init; }

    /// <summary>
    /// Идентификатор пользователя или системного субъекта, выполнившего назначение
    /// </summary>
    public Guid AssignedById { get; init; }

    /// <summary>
    /// Время назначения исполнителя в UTC
    /// </summary>
    public DateTime AssignedAt { get; init; }

    /// <summary>
    /// Код источника назначения исполнителя
    /// </summary>
    public string AssignmentSource { get; init; } = string.Empty;
}