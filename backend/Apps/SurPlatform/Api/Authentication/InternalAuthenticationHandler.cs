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
internal sealed class InternalAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ClientDatabaseContext _database;
    private readonly IConfiguration _configuration;
    
    ///
    public InternalAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder,
        ClientDatabaseContext database,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _database = database;
        _configuration = configuration;
    }

    /// <inheritdoc/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!TryReadKey(out var key))
        {
            return Task.FromResult(AuthenticateResult.Fail("Служебное удостоверение отклонено"));
        }

        var expected = _configuration.GetSection("Identity:InternalKeys")[_database.Client.Host];

        if (string.IsNullOrWhiteSpace(expected) || !KeysMatch(expected, key))
        {
            return Task.FromResult(AuthenticateResult.Fail("Служебное удостоверение отклонено"));
        }

        var identity = new ClaimsIdentity(authenticationType: Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    /// Извлекает ключ из заголовка с поддерживаемой схемой аутентификации
    /// </summary>
    private bool TryReadKey(out string key)
    {
        key = string.Empty;

        if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization.ToString(), out var credential))
        {
            return false;
        }

        if (!string.Equals(credential.Scheme, SystemRequestHttpHandler.AuthenticationScheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (credential.Parameter is not { } parameter)
        {
            return false;
        }

        key = parameter;
        return true;
    }

    /// <summary>
    /// Сравнивает ключи с использованием криптографического сравнения
    /// </summary>
    private static bool KeysMatch(string expected, string provided)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);

        return CryptographicOperations.FixedTimeEquals(
            expectedBytes,
            providedBytes);
    }
}