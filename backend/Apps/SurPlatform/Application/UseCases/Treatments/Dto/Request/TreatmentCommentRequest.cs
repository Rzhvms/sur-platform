namespace Application.UseCases.Treatments.Dto.Request;

/// <summary>Поля команды работы с мерой</summary>
public sealed record TreatmentCommentRequest
{
    /// <summary>Версия меры или запроса</summary>
    public Guid Version { get; init; }

    /// <summary>Текст комментария</summary>
    public string Text { get; init; } = string.Empty;
}
