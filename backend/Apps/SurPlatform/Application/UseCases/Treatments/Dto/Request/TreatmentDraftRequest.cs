namespace Application.UseCases.Treatments.Dto.Request;

/// <summary>Поля команды работы с мерой</summary>
public sealed record TreatmentDraftRequest
{
    /// <summary>Версия меры или запроса</summary>
    public Guid Version { get; init; }

    /// <summary>Выбранная активная связь</summary>
    public Guid? RiskProcessId { get; init; }

    /// <summary>Название меры</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Описание</summary>
    public string? Description { get; init; }

    /// <summary>Календарная дата срока</summary>
    public DateOnly DueDate { get; init; }

    /// <summary>Плановая стоимость</summary>
    public decimal? CostPlanned { get; init; }

    /// <summary>Фактическая стоимость</summary>
    public decimal? CostActual { get; init; }
}
