namespace Domain.DalModels;

/// <summary>Хранение данных этапа выполнения мер</summary>
public sealed record TreatmentAssignmentDal
{
    /// <summary>Идентификатор текущего назначения</summary>
    public Guid Id { get; init; }

    /// <summary>Мера</summary>
    public Guid TreatmentId { get; init; }

    /// <summary>Сотрудник-исполнитель</summary>
    public Guid AssigneeId { get; init; }

    /// <summary>Назначивший пользователь</summary>
    public Guid AssignedById { get; init; }

    /// <summary>Момент назначения UTC</summary>
    public DateTime AssignedAt { get; init; }

    /// <summary>Источник назначения</summary>
    public string AssignmentSource { get; init; } = string.Empty;
}
