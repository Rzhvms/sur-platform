using Infrastructure.Services.Jira.Interfaces;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Constants;
using Application.Exceptions;

namespace Infrastructure.Services.Jira;

/// <summary>Выполняет ограниченные HTTPS-запросы Jira Data Center</summary>
internal sealed class JiraRestClient : IJiraRestClient
{
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

    /// <inheritdoc/>
    public async Task<JsonElement> RequestAsync(Uri baseUri, string token, string path, CancellationToken cancellation,
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

    /// <inheritdoc/>
    public async Task<bool> CanAssignAsync(Uri address, string token, string project, string assignee, CancellationToken cancellation)
    {
        const string assignablePath = "user/assignable/search";
        var users = await RequestAsync(address, token,
            $"{assignablePath}?project={Uri.EscapeDataString(project)}&username={Uri.EscapeDataString(assignee)}", cancellation);
        return users.EnumerateArray().Any(user => user.GetProperty("active").GetBoolean()
            && user.GetProperty("name").GetString() == assignee);
    }

    /// <inheritdoc/>
    public async Task<JsonElement[]> MetadataAsync(Uri baseUri, string token, string path, CancellationToken cancellation)
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

    /// <inheritdoc/>
    public void Dispose()
    {
        _http.Dispose();
    }
}
