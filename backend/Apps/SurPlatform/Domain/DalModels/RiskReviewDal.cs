namespace Domain.DalModels;

/// <summary>Независимый результат первичной обработки и человеческого пересмотра</summary>
public sealed record RiskReviewDal
{
    /// <summary>Запись пересмотра</summary>
    public Guid Id { get; init; }

    /// <summary>Первоначальный запуск</summary>
    public Guid ReviewRunId { get; init; }

    /// <summary>Риск</summary>
    public Guid RiskId { get; init; }

    /// <summary>Текущий итог пересмотра</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Время первичной обработки UTC</summary>
    public DateTime ProcessedAt { get; init; }

    /// <summary>Безопасный код ошибки обработки</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Последняя предыдущая оценка</summary>
    public Guid? PreviousAssessmentId { get; init; }

    /// <summary>Оценка, завершившая пересмотр</summary>
    public Guid? NewAssessmentId { get; init; }

    /// <summary>Время открытия UTC</summary>
    public DateTime? StartedAt { get; init; }

    /// <summary>Кто открыл пересмотр</summary>
    public Guid? StartedById { get; init; }

    /// <summary>Время завершения UTC</summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>Кто завершил пересмотр</summary>
    public Guid? CompletedById { get; init; }

    /// <summary>Причина завершения закрытием</summary>
    public string? CloseReason { get; init; }
}
