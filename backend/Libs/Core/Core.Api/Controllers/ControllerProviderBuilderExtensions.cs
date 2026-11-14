using Asp.Versioning;
using Core.Api.Controllers.ControllerTypes.Constants;
using Core.Api.Controllers.ControllerTypes.Conventions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Api.Controllers;

public static class ControllerProviderBuilderExtensions
{
    private const string ApiVersionGroupNameFormat = "'v'VVV";

    /// <summary>
    /// Регистрация зависимостей
    /// </summary>
    public static void AddCoreControllers(this IServiceCollection services, string adminRight = ControllerPolicies.AdminRight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adminRight);

        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = ApiVersion.Default;
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = ApiVersionGroupNameFormat;
                options.SubstituteApiVersionInUrl = true;
            });

        services.Configure<MvcOptions>(options =>
        {
            options.Conventions.Add(new ControllerRouteConvention());
        });
        
        services.AddAuthorization(options =>
        {
            options.AddPolicy(ControllerPolicies.Provider, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(ControllerPolicies.RightsClaimType, adminRight);
            });
        });
    }
}