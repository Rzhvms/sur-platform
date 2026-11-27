using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Constants;
using Api.Controllers.Public.Jira.Dto.Request;
using Application.Constants;
using Application.UseCases.Jira.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Api.Filters;

/// <summary>
/// Проверяет HTTP-реквизиты Jira до чтения тела и готовит вход прикладной команды
/// </summary>
internal sealed class JiraWebhookRequestFilter(IJiraUseCaseManager useCases) : IAsyncResourceFilter
{
    /// <summary>
    /// Имя служебного пользователя входящего обмена
    /// </summary>
    private const string LoginPrefix = "jira:";

    /// <summary>
    /// Поле задачи в теле входящего сигнала
    /// </summary>
    private const string IssueProperty = "issue";

    /// <summary>
    /// Поле стабильного идентификатора задачи
    /// </summary>
    private const string IssueIdProperty = "id";

    /// <summary>
    /// Максимальная длина цифрового идентификатора задачи
    /// </summary>
    private const int MaxIssueIdLength = 32;

    /// <summary>
    /// Лимит кодированного заголовка Basic
    /// </summary>
    private static readonly int MaxAuthorization = (LoginPrefix.Length + JiraRules.MaxWebhookSecretLength * 3 + 2) / 3 * 4;

    /// <inheritdoc/>
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (!request.IsHttps || !AuthenticationHeaderValue.TryParse(request.Headers.Authorization.ToString(), out var auth)
            || !string.Equals(auth.Scheme, JiraRules.Basic, StringComparison.OrdinalIgnoreCase)
            || auth.Parameter is not { Length: > 0 } encoded || encoded.Length > MaxAuthorization)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        string credential;
        try
        {
            credential = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch (FormatException)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (!credential.StartsWith(LoginPrefix, StringComparison.Ordinal))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var secret = credential[LoginPrefix.Length..];
        await useCases.AuthorizeJiraWebhookAsync(secret);
        try
        {
            using var json = await JsonDocument.ParseAsync(request.Body, cancellationToken: context.HttpContext.RequestAborted);
            if (json.RootElement.ValueKind != JsonValueKind.Object ||
                !json.RootElement.TryGetProperty(IssueProperty, out var issue) || issue.ValueKind != JsonValueKind.Object ||
                !issue.TryGetProperty(IssueIdProperty, out var id) || id.ValueKind != JsonValueKind.String ||
                id.GetString() is not { Length: > 0 and <= MaxIssueIdLength } issueId || !issueId.All(char.IsAsciiDigit))
            {
                context.Result = new BadRequestResult();
                return;
            }

            context.HttpContext.Items[typeof(JiraWebhookRequest)] = new JiraWebhookRequest { Secret = secret, IssueId = issueId };
        }
        catch (JsonException)
        {
            context.Result = new BadRequestResult();
            return;
        }
        catch (BadHttpRequestException error) when (error.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status413PayloadTooLarge);
            return;
        }

        await next();
    }
}