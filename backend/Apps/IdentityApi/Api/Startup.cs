using Api.Swagger;
using Application.Constants;
using Core.Api.Controllers.ControllerTypes.Constants;
using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;
using Application;
using Core.Api.Controllers;
using Core.Database.Migrations;
using Core.Api.User;
using Core.Api.Middlewares;
using Infrastructure.Services;
using Infrastructure;
using Infrastructure.Services.Jwt.RsaKeys.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace Api;

public sealed class Startup(IWebHostEnvironment env, IConfiguration configuration)
{
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
    public void ConfigureServices(IServiceCollection services)
    {
        var connectionString = Configuration.GetConnectionString("DefaultConnection");
        
        services.AddControllers();
        services.AddApplication();
        services.AddInfrastructure(connectionString ?? throw new InvalidOperationException("Не задана строка подключения DefaultConnection"));
        
        services.AddCoreControllers();
        services.AddCoreUser();
        
        services.AddIdentityServices(Configuration);
        
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

                    ValidIssuer = Configuration["Jwt:AccessTokenSettings:Issuer"],
                    ValidAudience = Configuration["Jwt:AccessTokenSettings:Audience"],

                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IRsaKeyProvider>((options, rsaKeyProvider) =>
            {
                options.TokenValidationParameters.IssuerSigningKey = rsaKeyProvider.ValidationKey;
            });
    
        services.AddAuthorization(options =>
        {
            options.AddPolicy(ControllerPolicies.Provider, policy =>
                policy.RequireAuthenticatedUser().RequireClaim(AccessTokenClaimTypes.Right, IdentityRights.ManageUsers));
        });
        
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder
                    .SetIsOriginAllowed(_ => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
        });

        services.AddEndpointsApiExplorer();

        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

        services.AddSwaggerGen(options =>
        {
            options.AddServer(new OpenApiServer
            {
                Url = "/identity"
            });
            options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
            {
                Name = "JWT Authentication",
                Type = SecuritySchemeType.Http,
                In = ParameterLocation.Header,
                Scheme = JwtBearerDefaults.AuthenticationScheme,
                BearerFormat = "JWT",
                Description = "Введите JWT токен в формате: {token}"
            });
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("bearer", document)] = []
            });
        });
    }

    /// <summary>
    /// Конфигурация middleware пайплайна
    /// </summary>
    public void Configure(IApplicationBuilder app, IApiVersionDescriptionProvider versionProvider)
    {
        if (Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                foreach (var description in versionProvider.ApiVersionDescriptions)
                {
                    options.SwaggerEndpoint(
                        $"/swagger/{description.GroupName}/swagger.json",
                        $"IdentityApi {description.GroupName}");
                }
            });
        }
        
        app.MigrateDatabase();

        app.UseMiddleware<ErrorHandlingMiddleware>();
        
        app.UseRouting();
        
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}
