using System.Text.Json.Serialization;

namespace Application.UseCases.Risks.Dto.Request;

/// <summary>Данные и версии формы расчёта</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveAssessmentRequest
{
    /// <summary>Версия риска</summary>
    public Guid Version { get; init; }

    /// <summary>Версия шкалы из предварительного расчёта</summary>
    public Guid ScaleVersionId { get; init; }

    /// <summary>Версия связанного объекта из предварительного расчёта</summary>
    public Guid? AssetVersion { get; init; }

    /// <summary>Входы расчёта</summary>
    public AssessmentRequest? Inputs { get; init; }
}
