using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Constants;
using Application.Exceptions;
using Domain.DalModels;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services.Jira;

/// <summary>Выполняет ограниченные HTTPS-запросы Jira Data Center</summary>
internal sealed class JiraRestClient : IDisposable
{
    /// <summary>Разрешённые базовые URL из конфигурации развёртывания</summary>
    private const string AllowedUrls = "Jira:AllowedBaseUrls";

    /// <summary>Поиск исполнителя Jira по переданному рабочему email</summary>
    internal const string UserSearchPath = "user/search?username=";

    /// <summary>Поле описания задачи, которое заполняется только при наличии описания меры</summary>
    internal const string DescriptionField = "description";

    /// <summary>Префикс REST API с учётом контекстного пути</summary>
    private const string ApiPath = "rest/api/2/";

    /// <summary>Максимальный размер ответа REST</summary>
    private const int MaxResponseBytes = 4 * 1024 * 1024;

    /// <summary>Запросы с запретом перенаправлений и cookies</summary>
    private readonly HttpClient _http = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false, UseCookies = false, UseProxy = false
    })
    {
        Timeout = TimeSpan.FromSeconds(8), MaxResponseContentBufferSize = MaxResponseBytes
    };

    /// <summary>Разрешает ровно один настроенный HTTPS URL с контекстным путём</summary>
    /// <param name="configuration">Конфигурация оператора</param>
    /// <param name="value">Запрошенный базовый URL</param>
    /// <returns>Проверенный базовый адрес</returns>
    /// <exception cref="IdentityCommandException">Адрес не разрешён</exception>
    internal static Uri Resolve(IConfiguration configuration, string value)
    {
        if (!Uri.TryCreate(value.TrimEnd('/') + "/", UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || !configuration.GetSection(AllowedUrls).GetChildren().Any(entry =>
                Uri.TryCreate(entry.Value?.TrimEnd('/') + "/", UriKind.Absolute, out var allowed)
                && uri.Equals(allowed) && !uri.IsLoopback))
        {
            throw new IdentityCommandException(400, JiraRules.Invalid);
        }
        return uri;
    }

    /// <summary>Проверяет идентификатор, используемый как сегмент URL Jira</summary>
    /// <param name="value">Ключ проекта, задачи или идентификатор</param>
    /// <returns>Признак безопасного сегмента</returns>
    internal static bool IsResourceId(string value)
    {
        return value.Length is > 0 and <= 128 && value.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_');
    }

    /// <summary>Читает ресурс Jira или отправляет задачу</summary>
    /// <param name="baseUri">Разрешённый адрес</param>
    /// <param name="token">REST-токен</param>
    /// <param name="path">Относительный путь Jira</param>
    /// <param name="cancellation">Отмена операции</param>
    /// <param name="fields">Поля создания задачи</param>
    /// <returns>JSON ответа</returns>
    /// <exception cref="HttpRequestException">Jira вернула ошибку или запрос не завершился</exception>
    internal async Task<JsonElement> RequestAsync(Uri baseUri, string token, string path, CancellationToken cancellation,
        Dictionary<string, object?>? fields = null)
    {
        using var request = new HttpRequestMessage(fields is null ? HttpMethod.Get : HttpMethod.Post,
            new Uri(baseUri, ApiPath + path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd("application/json");
        if (fields is not null)
        {
            request.Content = JsonContent.Create(new { fields });
        }
        using var response = await _http.SendAsync(request, cancellation);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellation);
        return json.RootElement.Clone();
    }

    /// <summary>Проверяет доступность назначения пользователя в проекте</summary>
    /// <param name="address">Разрешённый адрес</param>
    /// <param name="token">REST-секрет</param>
    /// <param name="project">Ключ проекта</param>
    /// <param name="assignee">Логин Jira</param>
    /// <param name="cancellation">Отмена проверки</param>
    /// <returns>Признак доступного активного исполнителя</returns>
    internal async Task<bool> CanAssignAsync(Uri address, string token, string project, string assignee, CancellationToken cancellation)
    {
        const string assignablePath = "user/assignable/search";
        var users = await RequestAsync(address, token,
            $"{assignablePath}?project={Uri.EscapeDataString(project)}&username={Uri.EscapeDataString(assignee)}", cancellation);
        return users.EnumerateArray().Any(user => user.GetProperty("active").GetBoolean()
            && user.GetProperty("name").GetString() == assignee);
    }

    /// <summary>Читает все страницы метаданных Jira</summary>
    /// <param name="baseUri">Разрешённый адрес</param>
    /// <param name="token">REST-токен</param>
    /// <param name="path">Путь метаданных</param>
    /// <param name="cancellation">Отмена чтения</param>
    /// <returns>Элементы страниц</returns>
    internal async Task<JsonElement[]> MetadataAsync(Uri baseUri, string token, string path, CancellationToken cancellation)
    {
        var result = new List<JsonElement>();
        while (result.Count < 10000)
        {
            var page = await RequestAsync(baseUri, token, path + "?maxResults=100&startAt=" +
                result.Count.ToString(CultureInfo.InvariantCulture), cancellation);
            var values = page.GetProperty("values");
            result.AddRange(values.EnumerateArray());
            if (values.GetArrayLength() == 0 ||
                page.TryGetProperty("total", out var total) && result.Count >= total.GetInt32() ||
                page.TryGetProperty("isLast", out var last) && last.GetBoolean())
            {
                return [..result];
            }
        }
        throw new IdentityCommandException(400, JiraRules.Invalid);
    }

    /// <summary>Выбирает поля создания, соответствующие календарной дате меры</summary>
    /// <param name="settings">Снимок подключения</param>
    /// <param name="measure">Мера для Jira</param>
    /// <param name="assignee">Логин исполнителя Jira</param>
    /// <returns>Поля создания Jira</returns>
    internal static Dictionary<string, object?> CreateFields(JiraSettingsDal settings, TreatmentDal measure, string assignee)
    {
        var fields = new Dictionary<string, object?>
        {
            ["project"] = new { key = settings.Project },
            ["issuetype"] = new { id = settings.IssueType },
            ["summary"] = measure.Title,
            ["assignee"] = new { name = assignee },
            ["duedate"] = DateOnly.FromDateTime(measure.DueDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };
        if (!string.IsNullOrWhiteSpace(measure.Description))
        {
            fields[DescriptionField] = measure.Description;
        }
        return fields;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _http.Dispose();
    }
}
