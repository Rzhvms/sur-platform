using Domain.DalModels;

namespace Application.Models;

/// <summary>Результат пересмотра и сохранённый контекст источника</summary>
/// <param name="Review">Состояние пересмотра риска</param>
/// <param name="Run">Снимок первоначального запуска</param>
public sealed record RiskReviewView(RiskReviewDal Review, ReviewRunDal Run);
