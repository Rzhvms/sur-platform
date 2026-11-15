using System.Text.Json;
using Application.Models;
using Domain.DalModels;

namespace Api.Controllers.Provider.Jira.Dto.Response;

/// <summary>Публичные параметры Jira без реквизитов доступа</summary>
public sealed record JiraSettingsResponse
{
    /// <summary>Ключ локальной записи</summary>
    public Guid Id { get; init; }

    /// <summary>Версия для конкурентного изменения</summary>
    public Guid Version { get; init; }

    /// <summary>Разрешённый адрес Jira Data Center</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>Ключ проекта Jira</summary>
    public string Project { get; init; } = string.Empty;

    /// <summary>Идентификатор типа задачи</summary>
    public string IssueType { get; init; } = string.Empty;

    /// <summary>Статус Jira для завершённой меры</summary>
    public string DoneStatus { get; init; } = string.Empty;

    /// <summary>Статус Jira для отложенной меры</summary>
    public string? HoldStatus { get; init; }

    /// <summary>Статус Jira для отменённой меры</summary>
    public string? CancelStatus { get; init; }

    /// <summary>Признак активного обмена</summary>
    public bool Enabled { get; init; }

    /// <summary>Ответственный за интеграцию</summary>
    public Guid ResponsibleUserId { get; init; }

    /// <summary>Автор последнего изменения</summary>
    public Guid UpdatedById { get; init; }

    /// <summary>Создатель настройки, если записан</summary>
    public Guid? CreatedById { get; init; }

    /// <summary>Текущие ожидающие и выполняемые отправки</summary>
    public long? PendingCount { get; init; }

    /// <summary>Неопределённые отправки для сверки</summary>
    public long? UnknownCount { get; init; }

    /// <summary>Адресное уведомление о задержке восстановительной отправки</summary>
    public bool RestorationRetryPending { get; init; }

    /// <summary>Календарная зона настройки</summary>
    public string TimeZone { get; init; } = string.Empty;

    /// <summary>Соответствия пользователей СУР и Jira</summary>
    public Dictionary<Guid, string> Executors { get; init; } = [];

    /// <summary>Формирует контракт для чтения и повторной отправки настроек</summary>
    /// <param name="row">Сохранённое подключение</param>
    /// <param name="administration">Согласованный снимок очереди при чтении панели</param>
    /// <returns>Настройка в формате HTTP</returns>
    internal static JiraSettingsResponse FromSettings(JiraSettingsDal row, JiraAdministrationView? administration = null)
    {
        return new JiraSettingsResponse
        {
            Id = row.Id,
            Version = row.Version,
            BaseUrl = row.BaseUrl,
            Project = row.Project,
            IssueType = row.IssueType,
            DoneStatus = row.DoneStatus,
            HoldStatus = row.HoldStatus,
            CancelStatus = row.CancelStatus,
            Enabled = row.Enabled,
            ResponsibleUserId = row.ResponsibleUserId,
            UpdatedById = row.UpdatedById,
            CreatedById = row.CreatedById,
            PendingCount = administration?.PendingCount,
            UnknownCount = administration?.UnknownCount,
            RestorationRetryPending = administration?.RestorationRetryPending == true,
            TimeZone = row.TimeZone,
            Executors = JsonSerializer.Deserialize<Dictionary<Guid, string>>(row.Executors) ?? []
        };
    }
}
