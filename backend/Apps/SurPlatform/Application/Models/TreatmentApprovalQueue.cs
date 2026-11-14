using Domain.DalModels;

namespace Application.Models;

/// <summary>Представление для работы с мерой</summary>
/// <param name="Items">Страница доступных запросов</param>
/// <param name="Count">Число ожидающих запросов</param>
public sealed record TreatmentApprovalQueue(TreatmentApprovalDal[] Items, long Count);
