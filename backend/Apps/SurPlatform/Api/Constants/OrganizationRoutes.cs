namespace Api.Constants;

/// <summary>
/// Пределы пагинации и поиска в административных справочниках
/// </summary>
internal static class OrganizationRoutes
{
    /// <summary>
    /// Максимальный размер страниц
    /// ы</summary>
    internal const int MaxPageSize = 100;

    /// <summary>
    /// Размер страницы по умолчанию
    /// </summary>
    internal const int DefaultPageSize = 10;

    /// <summary>
    /// Максимальная длина строки поиска
    /// </summary>
    internal const int MaxSearchLength = 250;
}