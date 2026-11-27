using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Core.Api.Handlers;
using Infrastructure.Clients;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Api.Authentication;

/// <summary>
/// Проверяет служебное удостоверение для выбранной клиентской базы
/// </summary>
/// <param name="options">Настройки схемы аутентификации</param>
/// <param name="logger">Фабрика журналов веб-хоста</param>
/// <param name="encoder">Кодировщик HTTP-ответов</param>
/// <param name="database">Клиентский контекст запроса</param>
/// <param name="configuration">Ключи служебных вызывающих</param>
internal sealed class InternalAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ClientDatabaseContext database,
    IConfiguration configuration) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>
    /// Секция с ключами по каноническим хостам клиентов
    /// </summary>
    private const string InternalKeysSection = "Identity:InternalKeys";

    /// <summary>
    /// Причина отказа без реквизитов и сведений о настройках клиента
    /// </summary>
    private const string InvalidCredentials = "Служебное удостоверение отклонено";

    /// <inheritdoc/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var expected = configuration.GetSection(InternalKeysSection)[database.Client.Host];
        if (string.IsNullOrWhiteSpace(expected) ||
            !AuthenticationHeaderValue.TryParse(Request.Headers.Authorization.ToString(), out var credential) ||
            !string.Equals(credential.Scheme, SystemRequestHttpHandler.AuthenticationScheme, StringComparison.OrdinalIgnoreCase) ||
            credential.Parameter is not { } key ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(key)))
        {
            return Task.FromResult(AuthenticateResult.Fail(InvalidCredentials));
        }

        var identity = new ClaimsIdentity(authenticationType: Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
