namespace Api.Constants;

/// <summary>Пути и границы пагинации операций риска</summary>
internal static class RiskRoutes
{
    /// <summary>Реестр и карточка риска</summary>
    internal const string Risks = "risks";

    /// <summary>Справочники для администратора и выбора в паспорте</summary>
    internal const string References = "risk-references";

    /// <summary>Административный журнал</summary>
    internal const string History = "business-history";

    /// <summary>Путь карточки</summary>
    internal const string ById = "{id:guid}";

    /// <summary>Набор справочных записей</summary>
    internal const string ByKind = "{kind}";

    /// <summary>Размер страницы по умолчанию</summary>
    internal const int DefaultPage = 25;

    /// <summary>Предельный размер страницы</summary>
    internal const int MaxPage = 100;

    /// <summary>Настройки и справочник метода оценки</summary>
    internal const string AssessmentSettings = "risk-assessment-settings";

    /// <summary>Опубликованная денежная шкала</summary>
    internal const string Scale = "impact-scale";

    /// <summary>Встроенная матрица</summary>
    internal const string Matrix = "matrix";

    /// <summary>Пороги маршрута принятия</summary>
    internal const string AcceptanceSettings = "acceptance";
}
