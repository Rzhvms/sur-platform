using Domain.DalModels;

namespace Application.Models;

/// <summary>Опубликованная шкала с её диапазонами</summary>
/// <param name="Version">Метаданные публикации</param>
/// <param name="Thresholds">Денежные диапазоны</param>
public sealed record ImpactScale(ImpactScaleDal Version, ImpactThresholdDal[] Thresholds);
