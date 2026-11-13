namespace Api.Attributes;

/// <summary>
/// Разрешает вызвать указанный endpoint с ограниченной сессией временного пароля
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class AllowPasswordChangeSessionAttribute : Attribute;
