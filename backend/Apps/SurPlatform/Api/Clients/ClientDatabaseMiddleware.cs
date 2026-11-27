using Api.Constants;
using Infrastructure.Clients;

namespace Api.Clients;

/// <summary>
/// Выбирает клиентскую базу до аутентификации и разрешения репозиториев
/// </summary>
internal sealed class ClientDatabaseMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Проверяет HTTPS-хост и первоначальную готовность базы
    /// </summary>
    /// <param name="http">Текущий HTTP-запрос</param>
    /// <param name="catalog">Разрешённые клиентские хосты</param>
    /// <param name="database">Контекст базы текущего запроса</param>
    /// <param name="initializer">Состояние подготовки клиентских баз</param>
    public async Task InvokeAsync(HttpContext http, ClientDatabaseCatalog catalog, ClientDatabaseContext database, ClientDatabaseInitializer initializer)
    {
        var client = catalog.Find(http.Request.Host.Host);
        
        if (client is null || !http.Request.IsHttps)
        {
            await Results.Problem(statusCode: StatusCodes.Status400BadRequest, 
                title: "Требуется зарегистрированный HTTPS-хост клиента").ExecuteAsync(http);
            
            return;
        }

        if (http.Request.Path == "/health/live")
        {
            await next(http);
            return;
        }

        database.Select(client);
        if (http.Request.Path != "/health/ready" && !initializer.IsPrepared(client))
        {
            await Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: IdentityHttp.DatabaseNotReady).ExecuteAsync(http);
            return;
        }

        await next(http);
    }
}