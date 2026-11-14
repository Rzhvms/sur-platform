namespace Core.Api.Controllers.ControllerTypes.Constants;

/// <summary>
/// Права доступа контроллеров
/// </summary>
public static class ControllerPolicies
{
    /// <summary>
    /// Политика административного доступа
    /// </summary>
    public const string Provider = "provider";

    /// <summary>
    /// Тип jwt-клейма с указанным правом доступа
    /// </summary>
    public const string RightsClaimType = "right";

    /// <summary>
    /// Код права администратора
    /// </summary>
    public const string AdminRight = "admin";
}