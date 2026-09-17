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
        services.AddControllers();
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

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}