using System.ComponentModel.DataAnnotations;

namespace Application.UseCases.Risks.Dto;

/// <summary>Данные RiskCommentRequest в клиентской базе</summary>
public sealed record RiskCommentRequest
{
    /// <summary>Текст комментария</summary>
    [Required, StringLength(16000)]
    public string Text { get; init; } = string.Empty;

    /// <summary>Версия комментария при редактировании</summary>
    public Guid? Version { get; init; }
}
