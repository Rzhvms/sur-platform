using Domain.DalModels;

namespace Application.Models;

/// <summary>Предварительный серверный расчёт и версии формы</summary>
/// <param name="RiskVersion">Версия карточки</param>
/// <param name="ScaleVersionId">Версия денежных границ</param>
/// <param name="AssetVersion">Версия объекта, если он участвует в расчёте</param>
/// <param name="Assessment">Результат расчёта</param>
/// <param name="AcceptanceLevel">Маршрут принятия</param>
public sealed record AssessmentPreview(Guid RiskVersion, Guid ScaleVersionId, Guid? AssetVersion, RiskAssessmentDal Assessment, string AcceptanceLevel);
