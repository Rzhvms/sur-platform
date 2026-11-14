using Api.UseCases;
using Api.Constants;
using Microsoft.Net.Http.Headers;
using Api.Authentication;
using Core.Api.Handlers;
using Microsoft.AspNetCore.Authentication;
using Api.Swagger;
using Api.Clients;
using Api.Attributes;
using Microsoft.AspNetCore.DataProtection;
using Application.Exceptions;
using Application.Settings;
using Infrastructure.Clients;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using Npgsql;
using System.Security.Claims;
using Application.Constants;
using Application.UseCases.Auth.interfaces;
using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;
using Application;
using Core.Api.Controllers;
using Core.Api.Controllers.ControllerTypes.Attributes;
using Core.Api.User;
using Core.Api.Middlewares;
using Infrastructure.Services;
using Infrastructure;
using Infrastructure.Services.Jwt.RsaKeys.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Api.Controllers.Public.Jira;
using Api.Controllers.Public.Risks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SameSiteMode = Microsoft.AspNetCore.Http.SameSiteMode;

namespace Api;

/// <summary>
/// Подключает модули, аутентификацию и HTTP-конвейер СУР
/// </summary>
/// <param name="env">Среда запуска веб-хоста</param>
/// <param name="configuration">Параметры запуска приложения</param>
public sealed class Startup(IWebHostEnvironment env, IConfiguration configuration)
{
    /// <summary>
    /// Адрес доверенного прокси в конфигурации
    /// </summary>
    private const string KnownProxyKey = "Http:KnownProxy";

    /// <summary>
    /// Jwt audience key
    /// </summary>
    private const string JwtAudienceKey = "Jwt:AccessTokenSettings:Audience";

    /// <summary>
    /// Missing session claims
    /// </summary>
    private const string MissingSessionClaims = "Токен не содержит идентификаторы пользователя и сессии";

    /// <summary>
    /// Stale access token
    /// </summary>
    private const string StaleAccessToken = "Требуется обновить токен доступа";

    /// <summary>
    /// Wrong issuer
    /// </summary>
    private const string WrongIssuer = "Токен выпущен для другого клиента";

    /// <summary>
    /// Data protection application
    /// </summary>
    private const string DataProtectionApplication = "SurPlatform";

    /// <summary>
    /// Data protection directory key
    /// </summary>
    private const string DataProtectionDirectoryKey = "Identity:DataProtectionDirectory";

    /// <summary>
    /// Bearer scheme id
    /// </summary>
    private const string BearerSchemeId = "bearer";

    /// <summary>
    /// Bearer display name
    /// </summary>
    private const string BearerDisplayName = "JWT Authentication";

    /// <summary>
    /// Jwt format
    /// </summary>
    private const string JwtFormat = "JWT";

    /// <summary>
    /// Bearer description
    /// </summary>
    private const string BearerDescription = "Введите JWT токен в формате: {token}";

    /// <summary>
    /// Problem code
    /// </summary>
    private const string ProblemCode = "code";

    /// <summary>Поле проблемного ответа со списком блокирующих связей</summary>
    private const string ProblemDependencyIds = "dependencyIds";

    /// <summary>
    /// Client database unavailable
    /// </summary>
    private const string ClientDatabaseUnavailable = "Клиентская база недоступна";

    /// <summary>
    /// Swagger path format
    /// </summary>
    private const string SwaggerPathFormat = "/swagger/{0}/swagger.json";

    /// <summary>
    /// Swagger title format
    /// </summary>
    private const string SwaggerTitleFormat = "SurPlatform {0}";

    /// <summary>
    /// Password change required
    /// </summary>
    private const string PasswordChangeRequired = "Требуется смена временного пароля";

    /// <summary>
    /// Описание служебного удостоверения в OpenAPI
    /// </summary>
    private const string InternalSecurityDescription = "Введите значение заголовка целиком в формате SurInternal <ключ клиента>";

    /// <summary>
    /// Конфигурация приложения
    /// </summary>
    private IConfiguration Configuration { get; } = configuration;

    /// <summary>
    /// Окружение приложения
    /// </summary>
    private IWebHostEnvironment Environment { get; } = env;

    /// <summary>
    /// Регистрация сервисов
    /// </summary>
    /// <param name="services">Регистрации веб-хоста</param>
    public void ConfigureServices(IServiceCollection services)
    {
        if (Environment.IsDevelopment())
        {
            services.AddHttpsRedirection(_ => { });
        }

        services.AddControllersWithViews(options =>
        {
            options.Filters.Add(new ProducesResponseTypeAttribute(
                typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable));
        });
        services.AddApplication();
        services.AddScoped<SystemUseCaseManager>();
        services.AddInfrastructure();
        services.AddSingleton(new ClientDatabaseCatalog(Configuration));
        services.AddSingleton<ClientDatabaseInitializer>();
        services.AddHostedService<ClientDatabaseInitializer>(
            provider => provider.GetRequiredService<ClientDatabaseInitializer>());

        var proxyAddress = Configuration[KnownProxyKey];
        if (proxyAddress is not null)
        {
            var proxy = IPAddress.Parse(proxyAddress);
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = 1;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
                options.KnownProxies.Add(proxy);
            });
        }

        services.AddCoreControllers(IdentityRights.Admin);
        services.AddCoreUser();

        services.AddIdentityServices(Configuration);
        services.AddScoped(provider =>
        {
            var defaults = provider.GetRequiredService<IOptions<JwtSettings>>().Value;
            return new JwtSettings
            {
                AccessTokenSettings = new AccessTokenSettings
                {
                    Issuer = provider.GetRequiredService<ClientDatabaseContext>().Client.Issuer,
                    Audience = defaults.AccessTokenSettings.Audience,
                    LifeTimeInSeconds = defaults.AccessTokenSettings.LifeTimeInSeconds
                },
                SessionLifetimeDays = defaults.SessionLifetimeDays,
                RefreshTokenSettings = defaults.RefreshTokenSettings
            };
        });

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, InternalAuthenticationHandler>(
                SystemRequestHttpHandler.AuthenticationScheme, _ => { })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],

                    ValidAudience = Configuration[JwtAudienceKey],

                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var userIdValue = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        var sessionIdValue = context.Principal?.FindFirst(AccessTokenClaimTypes.SessionId)?.Value;

                        if (!Guid.TryParse(userIdValue, out var userId) || userId == Guid.Empty ||
                            !Guid.TryParse(sessionIdValue, out var sessionId) || sessionId == Guid.Empty)
                        {
                            context.Fail(MissingSessionClaims);
                            return;
                        }

                        var version = context.Principal?.FindFirst(AccessTokenClaimTypes.AccessVersion)?.Value;
                        if (!long.TryParse(version, out var accessVersion) || accessVersion < 1)
                        {
                            context.Fail(StaleAccessToken);
                            return;
                        }

                        var authUseCaseManager = context.HttpContext.RequestServices.GetRequiredService<IAuthUseCaseManager>();
                        var isActive = await authUseCaseManager.IsSessionActiveAsync(
                            userId, sessionId, context.HttpContext.RequestAborted, accessVersion);

                        if (!isActive)
                        {
                            context.Fail(IdentityMessages.InvalidSession);
                        }
                    }
                };
            });

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IRsaKeyProvider, IHttpContextAccessor>((options, rsaKeyProvider, http) =>
            {
                options.TokenValidationParameters.IssuerSigningKey = rsaKeyProvider.ValidationKey;
                options.TokenValidationParameters.IssuerValidator = (issuer, _, _) =>
                {
                    var client = http.HttpContext?.RequestServices.GetRequiredService<ClientDatabaseContext>().Client;
                    return client is not null && string.Equals(issuer, client.Issuer, StringComparison.Ordinal)
                        ? issuer
                        : throw new SecurityTokenInvalidIssuerException(WrongIssuer);
                };
            });

        services.AddAuthorization();

        var dataProtection = services.AddDataProtection().SetApplicationName(DataProtectionApplication);
        if (Configuration[DataProtectionDirectoryKey] is { } keyDirectory)
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
        }

        services.AddAntiforgery(options =>
        {
            options.HeaderName = IdentityHttp.CsrfHeader;
            options.Cookie.Name = IdentityHttp.CsrfCookie;
            options.Cookie.Path = IdentityHttp.RootPath;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });

        services.AddEndpointsApiExplorer();

        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

        services.AddSwaggerGen(options =>
        {
            options.OperationFilter<IdentityOperationFilter>();
            options.DocumentFilter<SpecialAuthorizationDocumentFilter>();
            options.AddSecurityDefinition(BearerSchemeId, new OpenApiSecurityScheme
            {
                Name = BearerDisplayName,
                Type = SecuritySchemeType.Http,
                In = ParameterLocation.Header,
                Scheme = JwtBearerDefaults.AuthenticationScheme,
                BearerFormat = JwtFormat,
                Description = BearerDescription
            });
            options.AddSecurityDefinition(JiraWebhookController.OpenApiScheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = Application.Constants.JiraRules.Basic.ToLowerInvariant()
            });
            options.AddSecurityDefinition(ReviewWebhookController.OpenApiScheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = JwtBearerDefaults.AuthenticationScheme
            });
            options.AddSecurityDefinition(SystemRequestHttpHandler.AuthenticationScheme, new OpenApiSecurityScheme
            {
                Name = HeaderNames.Authorization,
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = InternalSecurityDescription
            });
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerSchemeId, document)] = []
            });
        });
    }

    /// <summary>
    /// Конфигурация middleware пайплайна
    /// </summary>
    /// <param name="app">Конвейер веб-хоста</param>
    /// <param name="versionProvider">Версии API для Swagger</param>
    public void Configure(IApplicationBuilder app, IApiVersionDescriptionProvider versionProvider)
    {
        if (Configuration[KnownProxyKey] is not null)
        {
            app.UseForwardedHeaders();
        }

        app.UseMiddleware<ErrorHandlingMiddleware>();
        app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (IdentityCommandException exception) when (!context.Response.HasStarted)
            {
                var extensions = new Dictionary<string, object?> { [ProblemCode] = exception.Code };
                if (exception.DependencyIds is { } dependencies)
                {
                    extensions[ProblemDependencyIds] = dependencies;
                }

                await Results.Problem(statusCode: exception.Status, title: exception.Code, extensions: extensions)
                    .ExecuteAsync(context);
            }
            catch (NpgsqlException exception) when (exception.IsTransient && !context.Response.HasStarted)
            {
                await Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: ClientDatabaseUnavailable).ExecuteAsync(context);
            }
        });
        if (Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                foreach (var description in versionProvider.ApiVersionDescriptions)
                {
                    options.SwaggerEndpoint(
                        string.Format(System.Globalization.CultureInfo.InvariantCulture, SwaggerPathFormat, description.GroupName),
                        string.Format(System.Globalization.CultureInfo.InvariantCulture, SwaggerTitleFormat, description.GroupName));
                }
            });
        }

        app.UseMiddleware<ClientDatabaseMiddleware>();
        app.UseRouting();
        if (Configuration[KnownProxyKey] is { } proxyAddress)
        {
            var proxy = IPAddress.Parse(proxyAddress);
            app.Use(async (context, next) =>
            {
                var remote = context.Connection.RemoteIpAddress;
                if (remote?.IsIPv4MappedToIPv6 == true)
                {
                    remote = remote.MapToIPv4();
                }

                if (proxy.Equals(remote) &&
                    context.GetEndpoint()?.Metadata.GetMetadata<InternalControllerAttribute>() is not null)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                await next(context);
            });
        }

        app.UseAuthentication();
        app.Use(async (context, next) =>
        {
            var endpoint = context.GetEndpoint();
            if (context.User.HasClaim(AccessTokenClaimTypes.PasswordChangeRequired, IdentityClaimValues.True) &&
                endpoint is not null &&
                endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null &&
                endpoint.Metadata.GetMetadata<AllowPasswordChangeSessionAttribute>() is null)
            {
                await Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                    title: PasswordChangeRequired).ExecuteAsync(context);
                return;
            }

            await next(context);
        });
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}
