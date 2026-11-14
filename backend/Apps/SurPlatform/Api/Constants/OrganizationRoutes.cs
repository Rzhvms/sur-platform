namespace Api.Constants;

/// <summary>Маршруты и пределы чтения административных справочников</summary>
internal static class OrganizationRoutes
{
    /// <summary>Путь к справочникам после префикса provider</summary>
    internal const string Root = "organization/";

    /// <summary>Путь к записи справочника</summary>
    internal const string ById = "{id:guid}";

    /// <summary>Путь к изменению состояния справочной записи</summary>
    internal const string State = ById + "/state";

    /// <summary>Максимальный размер страницы</summary>
    internal const int MaxPageSize = 100;

    /// <summary>Размер страницы по умолчанию</summary>
    internal const int DefaultPageSize = 10;

    /// <summary>Максимальная длина строки поиска</summary>
    internal const int MaxSearchLength = 250;
}
