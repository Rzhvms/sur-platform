using Application.UseCases.Users.Dto.Request;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Api.Swagger;

/// <summary>Описывает временный пароль только как секретное входное поле</summary>
public sealed class ManualAccessSchemaFilter : ISchemaFilter
{
    /// <summary>JSON-имя секрета в административных командах</summary>
    private const string TemporaryPasswordProperty = "temporaryPassword";

    /// <summary>Формат поля для скрытого ввода в Swagger</summary>
    private const string PasswordFormat = "password";

    /// <summary>Пояснение ручной передачи временного доступа</summary>
    private const string PasswordDescription = "Временный пароль администратора: 8–256 символов, 10 дней, обязательная смена при первом входе. Передаётся пользователю самостоятельно и не возвращается в ответе";

    /// <inheritdoc/>
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type != typeof(CreateIdentityUserRequest) &&
            context.Type != typeof(ActivateIdentityUserRequest) &&
            context.Type != typeof(IssueIdentityAccessRequest))
        {
            return;
        }

        if (schema.Properties is not null &&
            schema.Properties.TryGetValue(TemporaryPasswordProperty, out var property) && property is OpenApiSchema password)
        {
            password.Format = PasswordFormat;
            password.WriteOnly = true;
            password.Description = PasswordDescription;
        }
    }
}
