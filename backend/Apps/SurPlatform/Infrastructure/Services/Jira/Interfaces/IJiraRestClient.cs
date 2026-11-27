using System.Text.Json;

namespace Infrastructure.Services.Jira.Interfaces;

/// <summary>
/// Исходящие REST-запросы к разрешенному подключению Jira
/// </summary>
public interface IJiraRestClient : IDisposable
{
    /// <summary>Читает ресурс Jira или отправляет задачу</summary>
    /// <param name="baseUri">Разрешённый адрес</param>
    /// <param name="token">REST-токен</param>
    /// <param name="path">Относительный путь Jira</param>
    /// <param name="cancellation">Отмена операции</param>
    /// <param name="fields">Поля создания задачи</param>
    /// <returns>JSON ответа</returns>
    /// <exception cref="HttpRequestException">Jira вернула ошибку или запрос не завершился</exception>
    Task<JsonElement> RequestAsync(Uri baseUri, string token, string path, CancellationToken cancellation,
        Dictionary<string, object?>? fields = null);

    /// <summary>Проверяет доступность назначения пользователя в проекте</summary>
    /// <param name="address">Разрешённый адрес</param>
    /// <param name="token">REST-секрет</param>
    /// <param name="project">Ключ проекта</param>
    /// <param name="assignee">Логин Jira</param>
    /// <param name="cancellation">Отмена проверки</param>
    /// <returns>Признак доступного активного исполнителя</returns>
    Task<bool> CanAssignAsync(Uri address, string token, string project, string assignee, CancellationToken cancellation);

    /// <summary>Читает все страницы метаданных Jira</summary>
    /// <param name="baseUri">Разрешённый адрес</param>
    /// <param name="token">REST-токен</param>
    /// <param name="path">Путь метаданных</param>
    /// <param name="cancellation">Отмена чтения</param>
    /// <returns>Элементы страниц</returns>
    Task<JsonElement[]> MetadataAsync(Uri baseUri, string token, string path, CancellationToken cancellation);
}
