using Api.Controllers.Public.Documents;
using Application.Constants;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Api.Swagger;

/// <summary>Описывает необработанный поток CSV и потоковые вложения</summary>
internal sealed class DocumentTransferFilter : IOperationFilter
{
    /// <summary>Тип произвольного потока байтов в OpenAPI</summary>
    private const string BinaryFormat = "binary";

    /// <summary>Ключ успешного ответа в контракте OpenAPI</summary>
    private const string Success = "200";

    /// <inheritdoc/>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(DocumentsController))
        {
            return;
        }

        if (context.MethodInfo.Name == nameof(DocumentsController.UploadAsync))
        {
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [DocumentRules.BinaryMediaType] = Binary()
                }
            };
            return;
        }

        if (context.MethodInfo.Name == nameof(DocumentsController.DownloadAsync) &&
            operation.Responses?.TryGetValue(Success, out var response) == true && response is OpenApiResponse concrete)
        {
            concrete.Content = new Dictionary<string, OpenApiMediaType>
            {
                [DocumentRules.CsvMediaType] = Binary(),
                [DocumentRules.ExcelMediaType] = Binary(),
                [DocumentRules.JsonMediaType] = Binary()
            };
        }
    }

    /// <summary>Описывает тело файла как поток байтов</summary>
    /// <returns>Схема тела вложения</returns>
    private static OpenApiMediaType Binary()
    {
        return new OpenApiMediaType
        {
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = BinaryFormat }
        };
    }
}
