namespace Application.Constants;

/// <summary>Основания, состояния и условия пересмотра</summary>
public static class ReviewRules
{
    /// <summary>Длина Bearer-секрета вебхука в шестнадцатеричных символах</summary>
    public const int TokenLength = 64;

    /// <summary>Размер первой страницы истории пересмотров в карточке риска</summary>
    public const int CardHistoryLimit = 20;

    /// <summary>Ручное основание</summary>
    public const string Manual = "MANUAL";

    /// <summary>Действие карточки</summary>
    public const string RiskCard = "RISK_CARD";

    /// <summary>Передача другому процессу</summary>
    public const string Transfer = "TRANSFER";

    /// <summary>Режим исторической оценки при передаче</summary>
    public const string TransferMode = "transfer";

    /// <summary>Право передачи риска</summary>
    public const string TransferRight = "transfer_risk";

    /// <summary>Основание после меры</summary>
    public const string AfterTreatment = "AFTER_TREATMENT";

    /// <summary>Источник после меры</summary>
    public const string Treatment = "TREATMENT";

    /// <summary>Календарное основание</summary>
    public const string Schedule = "SCHEDULE";

    /// <summary>Календарная настройка</summary>
    public const string ReviewSchedule = "REVIEW_SCHEDULE";

    /// <summary>Внешнее основание</summary>
    public const string Webhook = "WEBHOOK";

    /// <summary>Настройка внешнего источника</summary>
    public const string ReviewWebhook = "REVIEW_WEBHOOK";

    /// <summary>Открытый пересмотр</summary>
    public const string Waiting = "WAITING";

    /// <summary>Завершённый пересмотр или запуск</summary>
    public const string Completed = "COMPLETED";

    /// <summary>Первичная обработка продолжается</summary>
    public const string Processing = "PROCESSING";

    /// <summary>Текущее состояние не требует пересмотра</summary>
    public const string NoAction = "NO_ACTION";

    /// <summary>Обработка завершилась ошибкой</summary>
    public const string Error = "ERROR";

    /// <summary>Часть целей завершилась ошибкой</summary>
    public const string Partial = "COMPLETED_WITH_ERRORS";

    /// <summary>Некорректная настройка</summary>
    public const string Invalid = "REVIEW_CONFIGURATION_INVALID";

    /// <summary>Ошибка отдельной цели без внутренних данных</summary>
    public const string ProcessingFailed = "REVIEW_PROCESSING_FAILED";

    /// <summary>Однократное расписание</summary>
    public const string Once = "NONE";

    /// <summary>Недельное расписание</summary>
    public const string Weekly = "WEEKLY";

    /// <summary>Месячное расписание</summary>
    public const string Monthly = "MONTHLY";

    /// <summary>Годовое расписание</summary>
    public const string Yearly = "YEARLY";

    /// <summary>Без даты окончания</summary>
    public const string Never = "NEVER";

    /// <summary>Окончание по числу запусков</summary>
    public const string AfterOccurrences = "AFTER_OCCURRENCES";

    /// <summary>Окончание по календарной дате</summary>
    public const string OnDate = "ON_DATE";
}
