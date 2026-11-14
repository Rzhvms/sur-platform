using System.Text.Json;
using Application.Models;
using Application.UseCases.Jira.Dto.Request;
using Domain.DalModels;

namespace Application.Ports.Repositories;

/// <summary>Операции обмена Jira в клиентском контексте</summary>
public interface IJiraRepository
{
    /// <summary>Читает метаданные Jira без сохранения реквизитов</summary>
    /// <param name="actor">Администратор клиента</param>
    /// <param name="request">Адрес и проект</param>
    /// <param name="cancellation">Отмена запроса</param>
    /// <returns>Каталоги проекта, типов и статусов</returns>
    Task<JsonElement> DiscoverJiraAsync(RiskActor actor, JiraDiscoveryRequest request, CancellationToken cancellation);

    /// <summary>Читает настройку</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <returns>Подтверждённый результат операции</returns>
    Task<JiraSettingsDal?> GetJiraAsync(RiskActor actor);

    /// <summary>Сохраняет проверенную настройку</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="request">Административная команда</param>
    /// <param name="cancellation">Отмена HTTP-запроса</param>
    /// <returns>Подтверждённый результат операции</returns>
    Task<JiraSettingsDal> SaveJiraAsync(RiskActor actor, JiraSettingsRequest request, CancellationToken cancellation);

    /// <summary>Читает журнал ошибок</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Подтверждённый результат операции</returns>
    Task<JiraErrorDal[]> ListJiraErrorsAsync(RiskActor actor, int offset, int limit);

    /// <summary>Разрешает неизвестный результат</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="id">Мера</param>
    /// <param name="request">Административная команда</param>
    /// <param name="cancellation">Отмена HTTP-запроса</param>
    /// <returns>Подтверждённый результат операции</returns>
    Task<JiraTreatmentView> ResolveJiraAsync(RiskActor actor, Guid id, JiraResolveRequest request, CancellationToken cancellation);

    /// <summary>Проверяет пароль Basic перед чтением запроса</summary>
    /// <param name="secret">Пароль вебхука</param>
    Task AuthorizeJiraWebhookAsync(string secret);

    /// <summary>Проверяет секрет и сохраняет сигнал</summary>
    /// <param name="secret">Пароль вебхука</param>
    /// <param name="issueId">Идентификатор задачи для повторного чтения</param>
    /// <returns>Подтверждённый результат операции</returns>
    Task<Guid> AcceptJiraSignalAsync(string secret, string issueId);
}
