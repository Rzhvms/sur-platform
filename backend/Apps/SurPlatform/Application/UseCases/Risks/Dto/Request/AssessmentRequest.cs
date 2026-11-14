using System.Text.Json.Serialization;

namespace Application.UseCases.Risks.Dto.Request;

/// <summary>Входы серверного расчёта</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssessmentRequest
{
    /// <summary>Уровень вероятности встроенного метода</summary>
    public Guid LikelihoodLevelId { get; init; }

    /// <summary>Сумма для риска без объекта</summary>
    public decimal? LossAmount { get; init; }

    /// <summary>Недели простоя</summary>
    public int? DowntimeWeeks { get; init; }

    /// <summary>Дни простоя</summary>
    public int? DowntimeDays { get; init; }

    /// <summary>Дополнительные часы простоя</summary>
    public decimal? DowntimeHours { get; init; }
}
