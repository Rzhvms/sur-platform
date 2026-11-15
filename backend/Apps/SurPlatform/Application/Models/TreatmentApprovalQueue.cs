using Domain.DalModels;

namespace Application.Models;

/// <summary>Представление для работы с мерой</summary>
/// <param name="Items">Страница доступных запросов</param>
/// <param name="Count">Число ожидающих запросов</param>
public sealed record TreatmentApprovalQueue(TreatmentApprovalDal[] Items, long Count)
{
    /// <summary>Названия мер по ID для запросов на текущей странице</summary>
    public Dictionary<Guid, string> TreatmentTitles { get; init; } = [];
}
