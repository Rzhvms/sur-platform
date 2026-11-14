namespace Application.Models;

/// <summary>Состояние задачи и доступность ручного управления</summary>
/// <param name="State">Состояние доставки</param>
/// <param name="IssueKey">Ключ подтверждённой задачи</param>
/// <param name="IssueUrl">Адрес подтверждённой задачи</param>
/// <param name="ManualControl">Признак доступности ручного управления по настройке</param>
/// <param name="AttemptStartedAt">Версия неопределённой попытки для подтверждения</param>
public sealed record JiraTreatmentView(string? State, string? IssueKey, string? IssueUrl, bool ManualControl, DateTime? AttemptStartedAt);
