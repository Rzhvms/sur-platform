using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Api.Swagger;

/// <summary>
/// Создает отдельный документ Swagger для каждой объявленной версии API
/// </summary>
public sealed class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private const string ApiTitle = "SurPlatform";

    private readonly IApiVersionDescriptionProvider _versionProvider;

    /// <summary>
    /// Создает настройку документов Swagger
    /// </summary>
    /// <param name="versionProvider">Источник объявленных версий API</param>
    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider versionProvider)
    {
        _versionProvider = versionProvider;
    }

    /// <inheritdoc/>
    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in _versionProvider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = ApiTitle,
                Version = description.ApiVersion.ToString()
            });
        }
    }
}
