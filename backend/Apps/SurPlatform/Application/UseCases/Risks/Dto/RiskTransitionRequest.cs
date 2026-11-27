using System.ComponentModel.DataAnnotations;

namespace Application.UseCases.Risks.Dto;

/// <summary>Данные RiskTransitionRequest в клиентской базе</summary>
public sealed record RiskTransitionRequest
{
    /// <summary>Ожидаемая версия риска</summary>
    public Guid Version { get; init; }

    /// <summary>Причина закрытия</summary>
    [StringLength(4000)]
    public string? Reason { get; init; }

    /// <summary>Подтверждение возврата закрытого риска</summary>
    public bool Confirmed { get; init; }
}
