namespace Application.Constants;

/// <summary>
/// Ограничения административного списка пользователей
/// </summary>
public static class IdentityQueryLimits
{
    /// <summary>
    /// Минимальный размер запрашиваемой страницы
    /// </summary>
    public const int MinPageSize = 1;

    /// <summary>
    /// Размер страницы по умолчанию
    /// </summary>
    public const int DefaultPageSize = 50;

    /// <summary>
    /// Максимальный размер запрашиваемой страницы
    /// </summary>
    public const int MaxPageSize = 100;
}