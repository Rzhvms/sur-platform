namespace Application.Constants;

/// <summary>Статусы, права и ошибки выполнения мер</summary>
public static class TreatmentRules
{
    /// <summary>Черновик</summary>
    public const string Draft = "DRAFT";

    /// <summary>На согласовании</summary>
    public const string Pending = "PENDING";

    /// <summary>Согласовано</summary>
    public const string Approved = "APPROVED";

    /// <summary>Ожидает выполнения</summary>
    public const string Waiting = "WAITING";

    /// <summary>В работе</summary>
    public const string Working = "IN_PROGRESS";

    /// <summary>Холд</summary>
    public const string Hold = "HOLD";

    /// <summary>Готово</summary>
    public const string Done = "DONE";

    /// <summary>Отменено</summary>
    public const string Cancelled = "CANCELLED";

    /// <summary>Отклонённый запрос</summary>
    public const string Rejected = "REJECTED";

    /// <summary>Отозванный запрос</summary>
    public const string Revoked = "REVOKED";

    /// <summary>Создание меры</summary>
    public const string Create = "create_treatment";

    /// <summary>Редактирование меры</summary>
    public const string Edit = "edit_treatment";

    /// <summary>Комментарии меры</summary>
    public const string Comment = "comment_treatment";

    /// <summary>Назначение исполнителя</summary>
    public const string Assign = "assign_treatment_executor";

    /// <summary>Ручные переходы</summary>
    public const string Execute = "execute_treatment";

    /// <summary>Согласование владельцем</summary>
    public const string ApproveOwner = "approve_owner_treatment";

    /// <summary>Согласование директором</summary>
    public const string ApproveDirector = "approve_director_treatment";

    /// <summary>Согласование уровня CEO</summary>
    public const string ApproveCeo = "approve_ceo_treatment";

    /// <summary>Роль сотрудника</summary>
    public const string Employee = "EMPLOYEE";

    /// <summary>Источник ручного назначения</summary>
    public const string Manual = "manual";

    /// <summary>Некорректная команда</summary>
    public const string Invalid = "TREATMENT_INVALID";

    /// <summary>Статус или условия не допускают команду</summary>
    public const string Conflict = "TREATMENT_STATE_CONFLICT";

    /// <summary>Слот процесса занят</summary>
    public const string SlotTaken = "TREATMENT_SLOT_TAKEN";

    /// <summary>Нет действующего согласующего</summary>
    public const string NoApprover = "TREATMENT_NO_APPROVER";

    /// <summary>Исполнитель не подходит</summary>
    public const string InvalidExecutor = "TREATMENT_INVALID_EXECUTOR";

    /// <summary>Необходимо подтвердить наличие черновиков</summary>
    public const string ConfirmationRequired = "TREATMENT_CONFIRMATION_REQUIRED";

    /// <summary>Создаёт список статусов, занимающих слот пары риск–процесс</summary>
    /// <returns>Статусы для проверки слота</returns>
    public static string[] SlotStatuses()
    {
        return [Pending, Approved, Waiting, Working];
    }

    /// <summary>Создаёт список незавершённых мер, учитываемых в цикле</summary>
    /// <returns>Нетерминальные статусы после черновика</returns>
    public static string[] ActiveStatuses()
    {
        return [Pending, Approved, Waiting, Working, Hold];
    }
}
