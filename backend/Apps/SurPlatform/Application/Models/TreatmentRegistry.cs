using Domain.DalModels;
using Domain.DalModels.Treatments;

namespace Application.Models;

/// <summary>Страница мер текущего клиента с названиями связанных сущностей</summary>
/// <param name="Items">Меры в порядке от новых к старым</param>
/// <param name="HasMore">Наличие следующей страницы</param>
public sealed record TreatmentRegistry(TreatmentDal[] Items, bool HasMore)
{
    /// <summary>Названия рисков для текущей страницы</summary>
    public Dictionary<Guid, string> RiskNames { get; init; } = [];

    /// <summary>Названия процессов, включая исторически неактивные</summary>
    public Dictionary<Guid, string> ProcessNames { get; init; } = [];
}
