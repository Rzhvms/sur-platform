using Domain.DalModels;

namespace Application.Models;

/// <summary>Карточка и признаки доступности действий</summary>
/// <param name="Data">Поле Data представления</param>
/// <param name="PassportEditable">Поле PassportEditable представления</param>
/// <param name="PreviousAssessment">Поле PreviousAssessment представления</param>
public sealed record RiskCard(RiskRow Data, bool PassportEditable, RiskAssessmentDal? PreviousAssessment)
{
    /// <summary>Первая страница истории с открытым пересмотром в начале</summary>
    public RiskReviewView[] Reviews { get; init; } = [];
}
