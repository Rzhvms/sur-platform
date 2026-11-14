namespace Application.Models;

/// <summary>Допустимый для нового риска процесс</summary>
/// <param name="Id">Процесс</param>
/// <param name="Name">Название процесса</param>
/// <param name="DirectorateId">Дирекция процесса</param>
/// <param name="OwnerId">Действующий владелец</param>
/// <param name="OwnerName">ФИО владельца</param>
public sealed record RiskProcessOption(Guid Id, string Name, Guid DirectorateId, Guid OwnerId, string? OwnerName);
