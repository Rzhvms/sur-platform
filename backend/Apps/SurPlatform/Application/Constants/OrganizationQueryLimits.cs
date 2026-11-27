namespace Application.Constants;

/// <summary>
/// Ограничения чтения справочников организационной структуры
/// </summary>
public static class OrganizationQueryLimits
{
    /// <summary>
    /// Минимальный размер запрашиваемой страницы
    /// </summary>
    public const int MinPageSize = 1;

    /// <summary>
    /// Размер страницы при отсутствии параметра запроса
    /// </summary>
    public const int DefaultPageSize = 10;

    /// <summary>
    /// Максимальный размер запрашиваемой страницы
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// Максимальная длина поисковой строки
    /// </summary>
    public const int MaxSearchLength = 250;
}