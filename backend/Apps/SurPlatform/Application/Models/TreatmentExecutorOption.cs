using Domain.DalModels;

namespace Application.Models;

/// <summary>Представление для работы с мерой</summary>
/// <param name="PersonId">Идентификатор сотрудника</param>
/// <param name="FullName">Имя для выбора исполнителя</param>
public sealed record TreatmentExecutorOption(Guid PersonId, string FullName);
