using Api.Constants;
using Api.Controllers.Public.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Api.Swagger;

/// <summary>
/// Описывает анонимные и защищённые CSRF операции в OpenAPI
/// </summary>
public sealed class IdentityOperationFilter : IOperationFilter
{
    /// <summary>
    /// Csrf description
    /// </summary>
    private const string CsrfDescription = "Токен из GET connect/browser/csrf. Отправляется вместе с CSRF-cookie";

    /// <summary>
    /// Bad request status
    /// </summary>
    private const string BadRequestStatus = "400";

    /// <summary>
    /// Csrf bad request description
    /// </summary>
    private const string CsrfBadRequestDescription = "Некорректный запрос или CSRF-токен";

    /// <inheritdoc/>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.IsDefined(typeof(AllowAnonymousAttribute), inherit: true) ||
            context.MethodInfo.DeclaringType?.IsDefined(typeof(AllowAnonymousAttribute), inherit: true) == true)
        {
            operation.Security = [];
        }

        if (context.MethodInfo.DeclaringType != typeof(BrowserAuthController) ||
            !string.Equals(context.ApiDescription.HttpMethod, IdentityHttp.Post, StringComparison.Ordinal))
        {
            return;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = IdentityHttp.CsrfHeader,
            In = ParameterLocation.Header,
            Required = true,
            Description = CsrfDescription,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        });
        operation.Responses ??= new OpenApiResponses();
        operation.Responses.TryAdd(BadRequestStatus, new OpenApiResponse { Description = CsrfBadRequestDescription });
    }
}
