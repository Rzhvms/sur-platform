using Api.Constants;
using Api.Controllers.Public.Jira;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc.Controllers;
using Api.Controllers.Internal.Users;
using Api.Controllers.Public.Risks;
using Core.Api.Handlers;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Api.Swagger;

/// <summary>
/// Описывает авторизацию проверки сессии и контракт вебхука пересмотра
/// </summary>
internal sealed class SpecialAuthorizationDocumentFilter : IDocumentFilter
{
    /// <summary>Описание необязательного тела вебхука в OpenAPI</summary>
    private const string WebhookBodyDescription = "Необязательное тело до 1 МиБ. Сервер сохраняет исходные байты";

    /// <summary>Формат произвольного потока байтов в OpenAPI 3.0</summary>
    private const string BinaryFormat = "binary";

    /// <summary>Корневое поле события Jira</summary>
    private const string JiraIssueProperty = "issue";

    /// <summary>Идентификатор задачи внутри события Jira</summary>
    private const string JiraIdProperty = "id";

    /// <inheritdoc/>
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        foreach (var description in context.ApiDescriptions)
        {
            if (description.ActionDescriptor is not ControllerActionDescriptor action ||
                description.RelativePath is not { } relativePath ||
                document.Paths is null ||
                !document.Paths.TryGetValue(IdentityHttp.RootPath + relativePath, out var path) ||
                path.Operations?.TryGetValue(System.Net.Http.HttpMethod.Post, out var operation) != true)
            {
                continue;
            }

            var type = action.ControllerTypeInfo.AsType();
            var scheme = type == typeof(SessionController) ? SystemRequestHttpHandler.AuthenticationScheme
                : type == typeof(ReviewWebhookController) ? ReviewWebhookController.OpenApiScheme
                : type == typeof(JiraWebhookController) ? JiraWebhookController.OpenApiScheme : null;
            if (scheme is null)
            {
                continue;
            }

            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(scheme, document)] = []
                }
            ];

            if (type == typeof(JiraWebhookController))
            {
                operation.RequestBody = new OpenApiRequestBody
                {
                    Required = true,
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        [MediaTypeNames.Application.Json] = new()
                        {
                            Schema = new OpenApiSchema
                            {
                                Type = JsonSchemaType.Object,
                                Required = new HashSet<string> { JiraIssueProperty },
                                Properties = new Dictionary<string, IOpenApiSchema>
                                {
                                    [JiraIssueProperty] = new OpenApiSchema
                                    {
                                        Type = JsonSchemaType.Object,
                                        Required = new HashSet<string> { JiraIdProperty },
                                        Properties = new Dictionary<string, IOpenApiSchema>
                                        {
                                            [JiraIdProperty] = new OpenApiSchema { Type = JsonSchemaType.String }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };
            }
            else if (type == typeof(ReviewWebhookController))
            {
                operation.RequestBody = new OpenApiRequestBody
                {
                    Description = WebhookBodyDescription,
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        [MediaTypeNames.Application.Json] = new() { Schema = new OpenApiSchema() },
                        [MediaTypeNames.Application.Octet] = new()
                        {
                            Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = BinaryFormat }
                        }
                    }
                };
            }
        }
    }
}
