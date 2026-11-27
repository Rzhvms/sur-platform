namespace Core.Api.Controllers.ControllerTypes.Attributes;

/// <summary>
/// Задает префикс маршрута для контроллера
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class ControllerPrefixAttribute : Attribute
{
    /// <summary>
    /// Префикс типа контроллера после версии API
    /// </summary>
    public string Prefix { get; }

    /// <summary>
    /// Задает префикс маршрута
    /// </summary>
    /// <param name="prefix">Префикс маршрута</param>
    public ControllerPrefixAttribute(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        Prefix = prefix.Trim('/');
    }
}
