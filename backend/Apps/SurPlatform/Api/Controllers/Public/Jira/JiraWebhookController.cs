using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.Constants;
using Application.UseCases.Jira;
using Core.Api.Controllers.ControllerTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Public.Jira;

/// <summary>Входящие сигналы Jira для клиента, выбранного по HTTPS-хосту</summary>
/// <param name="useCases">Авторизация и очередь событий</param>
[AllowAnonymous]
[Route(WebhookRoute)]
public sealed class JiraWebhookController(JiraUseCaseManager useCases) : BasePublicController
{
    /// <summary>Путь входящего сигнала Jira</summary>
    private const string WebhookRoute = "jira/webhook";

    /// <summary>Идентификатор независимой схемы OpenAPI</summary>
    internal const string OpenApiScheme = "jiraWebhookBasic";

    /// <summary>Имя служебного пользователя входящего обмена</summary>
    private const string LoginPrefix = "jira:";

    /// <summary>Лимит тела входящего запроса</summary>
    private const int MaxBody = 1024 * 1024;

    /// <summary>Лимит кодированного заголовка Basic</summary>
    private static readonly int MaxAuthorization = ((LoginPrefix.Length + JiraRules.MaxWebhookSecretLength * 3 + 2) / 3) * 4;

    /// <summary>Читает идентификатор задачи и фиксирует запрос на её повторное чтение</summary>
    /// <param name="cancellation">Отмена чтения</param>
    /// <returns>Идентификатор принятого сигнала</returns>
    [HttpPost]
    [RequestSizeLimit(MaxBody)]
    [ProducesResponseType<Guid>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<Guid>> ReceiveAsync(CancellationToken cancellation)
    {
        if (!Request.IsHttps || !AuthenticationHeaderValue.TryParse(Request.Headers.Authorization.ToString(), out var auth)
            || !string.Equals(auth.Scheme, JiraRules.Basic, StringComparison.OrdinalIgnoreCase)
            || auth.Parameter is not { Length: > 0 } encoded || encoded.Length > MaxAuthorization)
        {
            return Unauthorized();
        }
        string credential;
        try
        {
            credential = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch (FormatException)
        {
            return Unauthorized();
        }
        if (!credential.StartsWith(LoginPrefix, StringComparison.Ordinal))
        {
            return Unauthorized();
        }
        var secret = credential[LoginPrefix.Length..];
        await useCases.AuthorizeJiraWebhookAsync(secret);
        try
        {
            using var json = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellation);
            if (json.RootElement.ValueKind != JsonValueKind.Object ||
                !json.RootElement.TryGetProperty("issue", out var issue) || issue.ValueKind != JsonValueKind.Object ||
                !issue.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String ||
                id.GetString() is not { Length: > 0 and <= 32 } issueId || !issueId.All(char.IsAsciiDigit))
            {
                return BadRequest();
            }

            var receipt = await useCases.AcceptJiraSignalAsync(secret, issueId);
            return StatusCode(StatusCodes.Status202Accepted, receipt);
        }
        catch (JsonException)
        {
            return BadRequest();
        }
        catch (BadHttpRequestException error) when (error.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }
    }
}
