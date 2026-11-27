namespace Core.Api.Controllers.ControllerTypes.Constants;

/// <summary>
/// Префиксы контроллеров
/// </summary>
internal static class ControllerPrefix
{
    /// <summary>
    /// Общий маршрут с обязательной версией api
    /// </summary>
    internal const string ApiVersionPrefix = "api/v{version:apiVersion}";

    /// <summary>
    /// Публичные
    /// </summary>
    internal const string PublicRoutePrefix = "public";

    /// <summary>
    /// Внутренние/системные/debug
    /// </summary>
    internal const string InternalRoutePrefix = "internal";

    /// <summary>
    /// Административные
    /// </summary>
    internal const string ProviderRoutePrefix = "provider";
}