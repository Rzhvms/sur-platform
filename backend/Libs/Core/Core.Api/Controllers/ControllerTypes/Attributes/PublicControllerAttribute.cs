namespace Core.Api.Controllers.ControllerTypes.Attributes;

/// <summary>
/// Обозначает контроллер запросов клиентского приложения
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class PublicControllerAttribute : Attribute
{
}
