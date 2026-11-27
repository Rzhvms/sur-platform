namespace Application.UseCases.Risks.Dto.Request;

/// <summary>Поля административной команды пересмотра</summary>
public sealed record RotateReviewTokenRequest
{
    /// <summary>Ожидаемая версия</summary>
    public Guid Version { get; init; }

    /// <summary>Подтверждение немедленного отзыва прежнего секрета</summary>
    public bool Confirmed { get; init; }
}
