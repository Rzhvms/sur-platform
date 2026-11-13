using System.Net.Http.Headers;

namespace Core.Api.Handlers;

/// <summary>
/// Добавляет служебное удостоверение запросам к настроенному HTTPS origin
/// </summary>
public sealed class SystemRequestHttpHandler : HttpClientHandler
{
    /// <summary>
    /// Схема служебной аутентификации
    /// </summary>
    public const string AuthenticationScheme = "SurInternal";

    /// <summary>
    /// HTTPS origin, которому разрешено отправлять ключ
    /// </summary>
    private readonly Uri _origin;

    /// <summary>
    /// Ключ клиента из доверенной конфигурации
    /// </summary>
    private readonly string _key;

    /// <summary>
    /// Ограничивает передачу ключа целевым HTTPS origin
    /// </summary>
    /// <param name="origin">Адрес внутреннего API</param>
    /// <param name="key">Настроенный ключ клиента</param>
    public SystemRequestHttpHandler(Uri origin, string key)
    {
        if (!origin.IsAbsoluteUri || origin.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException(nameof(origin));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _origin = origin;
        _key = key;
        AllowAutoRedirect = false;
        UseCookies = false;
    }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var target = request.RequestUri;
        if (target is null || !target.IsAbsoluteUri ||
            Uri.Compare(_origin, target, UriComponents.SchemeAndServer, UriFormat.SafeUnescaped,
                StringComparison.OrdinalIgnoreCase) != 0)
        {
            throw new ArgumentException(nameof(request));
        }

        request.Headers.Authorization = new AuthenticationHeaderValue(AuthenticationScheme, _key);
        return base.SendAsync(request, cancellationToken);
    }
}
