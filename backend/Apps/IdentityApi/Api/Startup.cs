using IdentityApi.Application;
using IdentityApi.Infrastructure;
using IdentityApi.Infrastructure.Services.Jwt.Rsa.Interfaces;
using IdentityApi.Infrastructure.Services.Jwt.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IdentityApi.Api;

/// <summary>
/// Настраивает сервисы и HTTP-конвейер приложения.
/// </summary>
internal sealed class Startup(IConfiguration configuration)
{
    /// <summary>
    /// Регистрация сервисов в DI
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddControllers();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>, IRsaKeyProvider>((options, jwtSettings, keyProvider) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidIssuer = jwtSettings.Value.Issuer,
                    ValidAudience = jwtSettings.Value.Audience,
                    IssuerSigningKey = keyProvider.ValidationKey,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ValidTypes = ["at+jwt"],
                    NameClaimType = "sub",
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization();
    }

    /// <summary>
    /// Конфигурация конвейера обработки запросов
    /// </summary>
    public void Configure(IApplicationBuilder app, IWebHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
        }

        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}