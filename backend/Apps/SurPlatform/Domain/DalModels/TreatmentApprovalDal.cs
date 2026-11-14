namespace Domain.DalModels;

/// <summary>Хранение данных этапа выполнения мер</summary>
public sealed record TreatmentApprovalDal
{
    /// <summary>Идентификатор записи</summary>
    public Guid Id { get; init; }

    /// <summary>Версия для конкурентной записи</summary>
    public Guid Version { get; init; }

    /// <summary>Мера</summary>
    public Guid TreatmentId { get; init; }

    /// <summary>Состояние запроса</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Уровень согласования</summary>
    public string RequiredLevel { get; init; } = string.Empty;

    /// <summary>Снимок стоимости</summary>
    public decimal CostPlanned { get; init; }

    /// <summary>Порог сработавшего правила</summary>
    public decimal TriggerThreshold { get; init; }

    /// <summary>Снимок нижнего порога владельца</summary>
    public decimal OwnerThreshold { get; init; }

    /// <summary>Снимок нижнего порога директора</summary>
    public decimal DirectorThreshold { get; init; }

    /// <summary>Снимок нижнего порога CEO</summary>
    public decimal CeoThreshold { get; init; }

    /// <summary>Инициатор</summary>
    public Guid RequestedById { get; init; }

    /// <summary>Момент отправки UTC</summary>
    public DateTime RequestedAt { get; init; }

    /// <summary>Принявший решение пользователь</summary>
    public Guid? DecidedById { get; init; }

    /// <summary>Момент решения UTC</summary>
    public DateTime? DecidedAt { get; init; }

    /// <summary>Комментарий или причина отклонения</summary>
    public string? DecisionReason { get; init; }

    /// <summary>Отозвавший пользователь</summary>
    public Guid? RevokedById { get; init; }

    /// <summary>Момент отзыва UTC</summary>
    public DateTime? RevokedAt { get; init; }

    /// <summary>Причина отзыва</summary>
    public string? RevokeReason { get; init; }
}
