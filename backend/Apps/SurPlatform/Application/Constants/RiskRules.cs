namespace Application.Constants;

/// <summary>Коды доступа, статусы и ошибки команд риска</summary>
public static class RiskRules
{
    /// <summary>Просмотр реестра, карточки и доски</summary>
    public const string View = "view_risk";

    /// <summary>Идентификация риска</summary>
    public const string Create = "create_risk";

    /// <summary>Редактирование паспорта и возврат</summary>
    public const string Edit = "edit_risk";

    /// <summary>Комментарии к риску</summary>
    public const string Comment = "comment_risk";

    /// <summary>Закрытие риска</summary>
    public const string Close = "close_risk";

    /// <summary>Черновик до первой оценки</summary>
    public const string Draft = "DRAFT";

    /// <summary>Активный риск</summary>
    public const string Active = "ACTIVE";

    /// <summary>Риск в работе</summary>
    public const string InProgress = "IN_PROGRESS";

    /// <summary>Риск ожидает переоценки</summary>
    public const string AwaitingReview = "AWAITING_REVIEW";

    /// <summary>Риск принят</summary>
    public const string Accepted = "ACCEPTED";

    /// <summary>Риск закрыт</summary>
    public const string Closed = "CLOSED";

    /// <summary>Запись не найдена</summary>
    public const string NotFound = "RISK_RESOURCE_NOT_FOUND";

    /// <summary>Недопустимые данные команды</summary>
    public const string Invalid = "RISK_INVALID";

    /// <summary>Запись недоступна для новой связи</summary>
    public const string InactiveReference = "RISK_REFERENCE_INACTIVE";

    /// <summary>Название занято активной записью</summary>
    public const string Duplicate = "RISK_REFERENCE_DUPLICATE";

    /// <summary>Паспорт защищён историей оценок</summary>
    public const string PassportLocked = "RISK_PASSPORT_LOCKED";

    /// <summary>Изменение комментария запрещено</summary>
    public const string CommentLocked = "RISK_COMMENT_LOCKED";

    /// <summary>Переход не разрешён для текущего состояния</summary>
    public const string InvalidTransition = "RISK_INVALID_TRANSITION";

    /// <summary>Оценка и переоценка</summary>
    public const string Assess = "assess_risk";

    /// <summary>Разделение риска</summary>
    public const string Split = "split_risk";

    /// <summary>Принятие владельцем</summary>
    public const string AcceptOwner = "accept_owner_risk";

    /// <summary>Принятие директором</summary>
    public const string AcceptDirector = "accept_director_risk";

    /// <summary>Принятие на уровне CEO</summary>
    public const string AcceptCeo = "accept_ceo_risk";

    /// <summary>Маршрут обязательной обработки</summary>
    public const string TreatmentRequired = "TREATMENT_REQUIRED";

    /// <summary>Режим обработки</summary>
    public const string TreatMode = "treat";

    /// <summary>Режим разделения</summary>
    public const string SplitMode = "split";

    /// <summary>Режим принятия</summary>
    public const string AcceptMode = "accept";

    /// <summary>Денежная шкала ещё не опубликована</summary>
    public const string ScaleRequired = "RISK_SCALE_REQUIRED";

    /// <summary>Способ работы уже выбран</summary>
    public const string ModeSelected = "RISK_MODE_SELECTED";

    /// <summary>У прежней оценки отсутствуют входы расчёта</summary>
    public const string AssessmentInputRequired = "RISK_ASSESSMENT_INPUT_REQUIRED";

    /// <summary>Порядок колонок канбан-доски</summary>
    public static IReadOnlyList<string> BoardStatuses { get; } =
        Array.AsReadOnly(new[] { Draft, Active, InProgress, AwaitingReview, Accepted, Closed });
}
