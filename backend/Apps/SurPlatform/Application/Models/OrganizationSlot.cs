namespace Application.Models;

/// <summary>
/// Состояние позиции на момент проверки в базе клиента
/// </summary>
/// <param name="ObservedAt">Момент наблюдения UTC</param>
/// <param name="AssignmentId">Действующее назначение, если позиция занята</param>
public sealed record OrganizationSlot(DateTimeOffset ObservedAt, Guid? AssignmentId);
