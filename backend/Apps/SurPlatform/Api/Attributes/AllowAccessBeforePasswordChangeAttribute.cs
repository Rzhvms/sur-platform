namespace Api.Attributes;

/// <summary>
/// Разрешает вызов endpoint пользователю, который еще не выполнил обязательную смену пароля
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class AllowAccessBeforePasswordChangeAttribute : Attribute;