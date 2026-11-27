using Domain.DalModels;

namespace Infrastructure.Repositories.Risks.Models;

/// <summary>Локальные данные, проверенные перед отправкой в Jira</summary>
/// <param name="Measure">Снимок меры</param>
/// <param name="AssignmentId">Текущее назначение</param>
/// <param name="AssignedAt">Версия назначения по времени записи</param>
/// <param name="Login">Сопоставленный логин Jira</param>
internal sealed record JiraCreateCandidate(TreatmentDal Measure, Guid AssignmentId, DateTime AssignedAt, string Login);
