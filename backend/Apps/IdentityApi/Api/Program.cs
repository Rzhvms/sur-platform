namespace IdentityApi.Api;

/// <summary>
/// Точка входа в приложение
/// </summary>
internal static class Program
{
    /// <summary>
    /// Запуск приложения
    /// </summary>
    public static async Task Main(string[] args)
    {
        await Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            })
            .Build()
            .RunAsync();
    }
}