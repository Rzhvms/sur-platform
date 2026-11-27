using Domain.DalModels.Base;

namespace Domain.DalModels.Treatments;

/// <summary>
/// Запрос согласования меры со снимком денежных оснований и принятым решением
/// </summary>
public sealed record TreatmentApprovalDal : VersionedDalModelBase
{
    /// <summary>
    /// Идентификатор согласуемой меры
    /// </summary>
    public Guid TreatmentId { get; init; }

    /// <summary>
    /// Код состояния запроса согласования
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Уровень полномочий, требуемый для согласования
    /// </summary>
    public string RequiredLevel { get; init; } = string.Empty;

    /// <summary>
    /// Плановые расходы на меру в рублях на момент запроса
    /// </summary>
    public decimal CostPlanned { get; init; }

    /// <summary>
    /// Денежный порог, определивший требуемый уровень согласования
    /// </summary>
    public decimal TriggerThreshold { get; init; }

    /// <summary>
    /// Нижний денежный порог владельца процесса на момент запроса
    /// </summary>
    public decimal OwnerThreshold { get; init; }

    /// <summary>
    /// Нижний денежный порог директора на момент запроса
    /// </summary>
    public decimal DirectorThreshold { get; init; }

    /// <summary>
    /// Нижний денежный порог генерального директора на момент запроса
    /// </summary>
    public decimal CeoThreshold { get; init; }

    /// <summary>
    /// Идентификатор пользователя, отправившего меру на согласование
    /// </summary>
    public Guid RequestedById { get; init; }

    /// <summary>
    /// Время отправки запроса согласования в UTC
    /// </summary>
    public DateTime RequestedAt { get; init; }

    /// <summary>
    /// Идентификатор пользователя, согласовавшего или отклонившего запрос
    /// </summary>
    public Guid? DecidedById { get; init; }

    /// <summary>
    /// Время принятия решения по запросу в UTC
    /// </summary>
    public DateTime? DecidedAt { get; init; }

    /// <summary>
    /// Комментарий к решению или обязательная причина отклонения
    /// </summary>
    public string? DecisionReason { get; init; }

    /// <summary>
    /// Идентификатор пользователя, отозвавшего согласование
    /// </summary>
    public Guid? RevokedById { get; init; }

    /// <summary>
    /// Время отзыва согласования в UTC
    /// </summary>
    public DateTime? RevokedAt { get; init; }

    /// <summary>
    /// Обоснование отзыва согласования
    /// </summary>
    public string? RevokeReason { get; init; }
}