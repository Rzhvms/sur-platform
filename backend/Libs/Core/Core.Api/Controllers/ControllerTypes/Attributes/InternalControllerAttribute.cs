namespace Core.Api.Controllers.ControllerTypes.Attributes;

/// <summary>
/// Обозначает контроллер внутренних запросов
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class InternalControllerAttribute : Attribute
{
}
