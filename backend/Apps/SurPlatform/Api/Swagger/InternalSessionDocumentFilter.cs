using Api.Constants;
using Microsoft.AspNetCore.Mvc.Controllers;
using Api.Controllers.Internal.Users;
using Core.Api.Handlers;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Api.Swagger;

/// <summary>
/// Указывает служебную схему авторизации для внутренней операции проверки сессии
/// </summary>
internal sealed class InternalSessionDocumentFilter : IDocumentFilter
{
    /// <inheritdoc/>
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        foreach (var description in context.ApiDescriptions)
        {
            if (description.ActionDescriptor is not ControllerActionDescriptor action ||
                action.ControllerTypeInfo.AsType() != typeof(SessionController) ||
                description.RelativePath is not { } relativePath ||
                document.Paths is null ||
                !document.Paths.TryGetValue(IdentityHttp.RootPath + relativePath, out var path) ||
                path.Operations?.TryGetValue(System.Net.Http.HttpMethod.Post, out var operation) != true)
            {
                continue;
            }

            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(SystemRequestHttpHandler.AuthenticationScheme, document)] = []
                }
            ];
        }
    }
}
