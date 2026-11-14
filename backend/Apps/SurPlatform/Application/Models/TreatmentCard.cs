using Domain.DalModels;

namespace Application.Models;

/// <summary>Представление для работы с мерой</summary>
/// <param name="Treatment">Мера</param>
/// <param name="Assignment">Текущее назначение</param>
/// <param name="LatestApproval">Последний запрос согласования</param>
/// <param name="RequiredApprovalLevel">Текущий маршрут для черновика, если нужен запрос</param>
public sealed record TreatmentCard(TreatmentDal Treatment, TreatmentAssignmentDal? Assignment,
    TreatmentApprovalDal? LatestApproval, string? RequiredApprovalLevel)
{
    /// <summary>Состояние обмена для меры</summary>
    public JiraTreatmentView? Jira { get; init; }
}
