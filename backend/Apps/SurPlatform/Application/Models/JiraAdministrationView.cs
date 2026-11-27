using Domain.DalModels;

namespace Application.Models;

/// <summary>Согласованный снимок настройки и очереди Jira для администратора</summary>
/// <param name="Settings">Подключение клиента</param>
/// <param name="PendingCount">Активные отправки, включая уже начатые</param>
/// <param name="UnknownCount">Задания, требующие ручной сверки</param>
/// <param name="RestorationRetryPending">Текущему администратору следует показать задержку восстановления</param>
public sealed record JiraAdministrationView(JiraSettingsDal Settings, long PendingCount, long UnknownCount, bool RestorationRetryPending);
