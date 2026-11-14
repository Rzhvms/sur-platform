namespace Application.UseCases.Treatments.Dto.Request;

/// <summary>Поля команды работы с мерой</summary>
public sealed record TreatmentActionRequest
{
    /// <summary>Версия меры или запроса</summary>
    public Guid Version { get; init; }

    /// <summary>Выбранный исполнитель</summary>
    public Guid? AssigneeId { get; init; }

    /// <summary>Фактическая стоимость при завершении</summary>
    public decimal? CostActual { get; init; }

    /// <summary>Комментарий решения или причина</summary>
    public string? Reason { get; init; }

    /// <summary>Подтверждение при оставшихся черновиках</summary>
    public bool Confirmed { get; init; }
}
