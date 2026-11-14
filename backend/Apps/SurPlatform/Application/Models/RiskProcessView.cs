using Domain.DalModels;

namespace Application.Models;

/// <summary>Связь риска с отображаемыми данными процесса</summary>
/// <param name="ProcessId">Поле ProcessId представления</param>
/// <param name="DirectorateId">Поле DirectorateId представления</param>
/// <param name="OwnerId">Поле OwnerId представления</param>
/// <param name="IsPrimary">Поле IsPrimary представления</param>
/// <param name="ProcessName">Поле ProcessName представления</param>
/// <param name="DirectorateName">Поле DirectorateName представления</param>
/// <param name="OwnerName">Поле OwnerName представления</param>
/// <param name="OwnerActive">Поле OwnerActive представления</param>
/// <param name="OwnerUserId">Учётная запись владельца</param>
public sealed record RiskProcessView(Guid ProcessId, Guid DirectorateId, Guid OwnerId, bool IsPrimary, string ProcessName, string DirectorateName, string? OwnerName, bool OwnerActive, Guid OwnerUserId);
