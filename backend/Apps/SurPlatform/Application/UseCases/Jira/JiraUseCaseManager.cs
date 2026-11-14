using System.Text.Json;
using Application.Models;
using Application.UseCases.Jira.Dto.Request;
using Domain.DalModels;

using Application.Ports.Repositories;

namespace Application.UseCases.Jira;

/// <summary>Прикладные команды Jira</summary>
/// <param name="repository">Клиентское хранилище интеграции</param>
public sealed class JiraUseCaseManager(IJiraRepository repository)
{
    /// <summary>Читает метаданные Jira без сохранения реквизитов</summary>
    /// <param name="actor">Администратор клиента</param>
    /// <param name="request">Адрес и проект</param>
    /// <param name="cancellation">Отмена запроса</param>
    /// <returns>Каталоги проекта, типов и статусов</returns>
    public Task<JsonElement> DiscoverJiraAsync(RiskActor actor, JiraDiscoveryRequest request, CancellationToken cancellation)
    {
        return repository.DiscoverJiraAsync(actor, request, cancellation);
    }

    /// <summary>Читает настройку</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <returns>Подтверждённый результат операции</returns>
    public Task<JiraSettingsDal?> GetJiraAsync(RiskActor actor)
    {
        return repository.GetJiraAsync(actor);
    }

    /// <summary>Сохраняет проверенную настройку</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="request">Административная команда</param>
    /// <param name="cancellation">Отмена HTTP-запроса</param>
    /// <returns>Подтверждённый результат операции</returns>
    public Task<JiraSettingsDal> SaveJiraAsync(RiskActor actor, JiraSettingsRequest request, CancellationToken cancellation)
    {
        return repository.SaveJiraAsync(actor, request, cancellation);
    }

    /// <summary>Читает журнал ошибок</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <returns>Подтверждённый результат операции</returns>
    public Task<JiraErrorDal[]> ListJiraErrorsAsync(RiskActor actor, int offset, int limit)
    {
        return repository.ListJiraErrorsAsync(actor, offset, limit);
    }

    /// <summary>Разрешает неизвестный результат</summary>
    /// <param name="actor">Текущий администратор</param>
    /// <param name="id">Мера</param>
    /// <param name="request">Административная команда</param>
    /// <param name="cancellation">Отмена HTTP-запроса</param>
    /// <returns>Подтверждённый результат операции</returns>
    public Task<JiraTreatmentView> ResolveJiraAsync(RiskActor actor, Guid id, JiraResolveRequest request, CancellationToken cancellation)
    {
        return repository.ResolveJiraAsync(actor, id, request, cancellation);
    }

    /// <summary>Проверяет пароль Basic перед чтением запроса</summary>
    /// <param name="secret">Пароль вебхука</param>
    public Task AuthorizeJiraWebhookAsync(string secret)
    {
        return repository.AuthorizeJiraWebhookAsync(secret);
    }

    /// <summary>Проверяет секрет и сохраняет сигнал</summary>
    /// <param name="secret">Пароль вебхука</param>
    /// <param name="issueId">Идентификатор задачи для повторного чтения</param>
    /// <returns>Подтверждённый результат операции</returns>
    public Task<Guid> AcceptJiraSignalAsync(string secret, string issueId)
    {
        return repository.AcceptJiraSignalAsync(secret, issueId);
    }
}
