using Domain.DalModels.Base;

namespace Domain.DalModels.Reviews;

/// <summary>
/// Результат обработки одного риска в запуске и последующего пересмотра пользователем
/// </summary>
public sealed record RiskReviewDal : DalModelBase
{
    /// <summary>
    /// Идентификатор исходного запуска пересмотра
    /// </summary>
    public Guid ReviewRunId { get; init; }

    /// <summary>
    /// Идентификатор пересматриваемого риска
    /// </summary>
    public Guid RiskId { get; init; }

    /// <summary>
    /// Код текущего результата пересмотра риска
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Время первичной обработки риска в запуске в UTC
    /// </summary>
    public DateTime ProcessedAt { get; init; }

    /// <summary>
    /// Безопасный код ошибки обработки риска
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Идентификатор оценки, предшествующей пересмотру
    /// </summary>
    public Guid? PreviousAssessmentId { get; init; }

    /// <summary>
    /// Идентификатор новой оценки, завершившей пересмотр
    /// </summary>
    public Guid? NewAssessmentId { get; init; }

    /// <summary>
    /// Время открытия пересмотра в UTC
    /// </summary>
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// Идентификатор пользователя или системного субъекта, открывшего пересмотр
    /// </summary>
    public Guid? StartedById { get; init; }

    /// <summary>
    /// Время завершения пересмотра в UTC
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Идентификатор пользователя или системного субъекта, завершившего пересмотр
    /// </summary>
    public Guid? CompletedById { get; init; }

    /// <summary>
    /// Причина закрытия риска, завершившего пересмотр
    /// </summary>
    public string? CloseReason { get; init; }
}