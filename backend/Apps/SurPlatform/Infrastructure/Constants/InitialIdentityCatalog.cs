using Application.Constants;

namespace Infrastructure.Constants;

/// <summary>
/// Снимок системного каталога для миграции 202611140100 и исходные данные синхронизации
/// </summary>
internal static class InitialIdentityCatalog
{
    /// <summary>
    /// Отображаемое название Admin
    /// </summary>
    internal const string AdminRoleName = "Администратор";

    /// <summary>
    /// Пояснение назначения роли Admin
    /// </summary>
    internal const string AdminDescription = "Полный доступ к администрированию и всем функциям системы";

    /// <summary>
    /// Системный код Ceo
    /// </summary>
    internal const string CeoCode = "CEO";

    /// <summary>
    /// Отображаемое название Ceo
    /// </summary>
    internal const string CeoName = "Генеральный директор";

    /// <summary>
    /// Пояснение назначения роли Ceo
    /// </summary>
    internal const string CeoDescription = "Принимает финальные решения по рискам уровня CEO, ключевым мерам и бюджетам мер";

    /// <summary>
    /// Системный код RiskSpecialist
    /// </summary>
    internal const string RiskSpecialistCode = "RISK_SPECIALIST";

    /// <summary>
    /// Отображаемое название RiskSpecialist
    /// </summary>
    internal const string RiskSpecialistName = "Специалист по оценке рисков";

    /// <summary>
    /// Пояснение назначения роли RiskSpecialist
    /// </summary>
    internal const string RiskSpecialistDescription = "Назначенный CEO сотрудник, который имеет права CEO для принятия решений в системе рисков, но не является CEO";

    /// <summary>
    /// Системный код Director
    /// </summary>
    internal const string DirectorCode = "DIRECTOR";

    /// <summary>
    /// Отображаемое название Director
    /// </summary>
    internal const string DirectorName = "Директор";

    /// <summary>
    /// Пояснение назначения роли Director
    /// </summary>
    internal const string DirectorDescription = "Согласует бюджет мер и принимает решения по рискам уровня держателя бюджета";

    /// <summary>
    /// Системный код Owner
    /// </summary>
    internal const string OwnerCode = "OWNER";

    /// <summary>
    /// Отображаемое название Owner
    /// </summary>
    internal const string OwnerName = "Владелец процесса";

    /// <summary>
    /// Пояснение назначения роли Owner
    /// </summary>
    internal const string OwnerDescription = "Управляет рисками и мерами в рамках своих процессов, оценивает и переоценивает риски, назначает исполнителей и согласовывает меры уровня OWNER";

    /// <summary>
    /// Системный код Employee
    /// </summary>
    internal const string EmployeeCode = "EMPLOYEE";

    /// <summary>
    /// Отображаемое название Employee
    /// </summary>
    internal const string EmployeeName = "Линейный сотрудник";

    /// <summary>
    /// Пояснение назначения роли Employee
    /// </summary>
    internal const string EmployeeDescription = "Идентифицирует риски, предлагает меры и может быть исполнителем меры";

    /// <summary>
    /// Системный код Admin
    /// </summary>
    internal const string AdminCode = IdentityRights.AdminCode;

    /// <summary>
    /// Отображаемое название Admin
    /// </summary>
    internal const string AdminName = "Полный административный доступ";

    /// <summary>
    /// Системный код ViewRisk
    /// </summary>
    internal const string ViewRiskCode = RiskRules.View;

    /// <summary>
    /// Отображаемое название ViewRisk
    /// </summary>
    internal const string ViewRiskName = "Просмотр рисков";

    /// <summary>
    /// Системный код CommentRisk
    /// </summary>
    internal const string CommentRiskCode = RiskRules.Comment;

    /// <summary>
    /// Отображаемое название CommentRisk
    /// </summary>
    internal const string CommentRiskName = "Комментирование риска";

    /// <summary>
    /// Системный код CommentTreatment
    /// </summary>
    internal const string CommentTreatmentCode = "comment_treatment";

    /// <summary>
    /// Отображаемое название CommentTreatment
    /// </summary>
    internal const string CommentTreatmentName = "Комментирование меры";

    /// <summary>
    /// Системный код CreateRisk
    /// </summary>
    internal const string CreateRiskCode = RiskRules.Create;

    /// <summary>
    /// Отображаемое название CreateRisk
    /// </summary>
    internal const string CreateRiskName = "Создание риска";

    /// <summary>
    /// Системный код EditRisk
    /// </summary>
    internal const string EditRiskCode = RiskRules.Edit;

    /// <summary>
    /// Отображаемое название EditRisk
    /// </summary>
    internal const string EditRiskName = "Редактирование риска";

    /// <summary>
    /// Системный код CloseRisk
    /// </summary>
    internal const string CloseRiskCode = RiskRules.Close;

    /// <summary>
    /// Отображаемое название CloseRisk
    /// </summary>
    internal const string CloseRiskName = "Закрытие риска";

    /// <summary>
    /// Системный код AssessRisk
    /// </summary>
    internal const string AssessRiskCode = RiskRules.Assess;

    /// <summary>
    /// Отображаемое название AssessRisk
    /// </summary>
    internal const string AssessRiskName = "Оценка и переоценка риска";

    /// <summary>
    /// Системный код AcceptOwnerRisk
    /// </summary>
    internal const string AcceptOwnerRiskCode = RiskRules.AcceptOwner;

    /// <summary>
    /// Отображаемое название AcceptOwnerRisk
    /// </summary>
    internal const string AcceptOwnerRiskName = "Принятие риска уровня владельца основного процесса";

    /// <summary>
    /// Системный код AcceptDirectorRisk
    /// </summary>
    internal const string AcceptDirectorRiskCode = RiskRules.AcceptDirector;

    /// <summary>
    /// Отображаемое название AcceptDirectorRisk
    /// </summary>
    internal const string AcceptDirectorRiskName = "Принятие риска уровня держателя бюджета";

    /// <summary>
    /// Системный код AcceptCeoRisk
    /// </summary>
    internal const string AcceptCeoRiskCode = RiskRules.AcceptCeo;

    /// <summary>
    /// Отображаемое название AcceptCeoRisk
    /// </summary>
    internal const string AcceptCeoRiskName = "Принятие риска уровня CEO";

    /// <summary>
    /// Системный код CreateTreatment
    /// </summary>
    internal const string CreateTreatmentCode = "create_treatment";

    /// <summary>
    /// Отображаемое название CreateTreatment
    /// </summary>
    internal const string CreateTreatmentName = "Создание меры";

    /// <summary>
    /// Системный код EditTreatment
    /// </summary>
    internal const string EditTreatmentCode = "edit_treatment";

    /// <summary>
    /// Отображаемое название EditTreatment
    /// </summary>
    internal const string EditTreatmentName = "Редактирование меры";

    /// <summary>
    /// Системный код AssignTreatmentExecutor
    /// </summary>
    internal const string AssignTreatmentExecutorCode = "assign_treatment_executor";

    /// <summary>
    /// Отображаемое название AssignTreatmentExecutor
    /// </summary>
    internal const string AssignTreatmentExecutorName = "Назначение ответственного исполнителя меры";

    /// <summary>
    /// Системный код ExecuteTreatment
    /// </summary>
    internal const string ExecuteTreatmentCode = "execute_treatment";

    /// <summary>
    /// Отображаемое название ExecuteTreatment
    /// </summary>
    internal const string ExecuteTreatmentName = "Выполнение меры";

    /// <summary>
    /// Системный код ApproveOwnerTreatment
    /// </summary>
    internal const string ApproveOwnerTreatmentCode = "approve_owner_treatment";

    /// <summary>
    /// Отображаемое название ApproveOwnerTreatment
    /// </summary>
    internal const string ApproveOwnerTreatmentName = "Согласование меры уровня владельца процесса";

    /// <summary>
    /// Системный код ApproveDirectorTreatment
    /// </summary>
    internal const string ApproveDirectorTreatmentCode = "approve_director_treatment";

    /// <summary>
    /// Отображаемое название ApproveDirectorTreatment
    /// </summary>
    internal const string ApproveDirectorTreatmentName = "Согласование меры уровня держателя бюджета";

    /// <summary>
    /// Системный код ApproveCeoTreatment
    /// </summary>
    internal const string ApproveCeoTreatmentCode = "approve_ceo_treatment";

    /// <summary>
    /// Отображаемое название ApproveCeoTreatment
    /// </summary>
    internal const string ApproveCeoTreatmentName = "Согласование меры уровня CEO";

    /// <summary>
    /// Системный код ViewReport
    /// </summary>
    internal const string ViewReportCode = "view_report";

    /// <summary>
    /// Отображаемое название ViewReport
    /// </summary>
    internal const string ViewReportName = "Просмотр отчётов";

    /// <summary>
    /// Системный код ExportReport
    /// </summary>
    internal const string ExportReportCode = "export_report";

    /// <summary>
    /// Отображаемое название ExportReport
    /// </summary>
    internal const string ExportReportName = "Экспорт отчётов";

    /// <summary>
    /// Системный код ImportData
    /// </summary>
    internal const string ImportDataCode = "import_data";

    /// <summary>
    /// Отображаемое название ImportData
    /// </summary>
    internal const string ImportDataName = "Импорт данных";

    /// <summary>
    /// Системный код SplitRisk
    /// </summary>
    internal const string SplitRiskCode = RiskRules.Split;

    /// <summary>
    /// Отображаемое название SplitRisk
    /// </summary>
    internal const string SplitRiskName = "Разделение риска по процессам";

    /// <summary>
    /// Системный код TransferRisk
    /// </summary>
    internal const string TransferRiskCode = Application.Constants.ReviewRules.TransferRight;

    /// <summary>
    /// Отображаемое название TransferRisk
    /// </summary>
    internal const string TransferRiskName = "Передача риска в другой процесс";

    /// <summary>
    /// Возвращает системные метаданные ролей без клиентских настроек
    /// </summary>
    /// <returns>Исходные роли по стабильным кодам</returns>
    internal static (string Code, string Name, string Description)[] GetRoles()
    {
        return
        [
            (IdentityAccounts.AdminRole, AdminRoleName, AdminDescription),
            (CeoCode, CeoName, CeoDescription),
            (RiskSpecialistCode, RiskSpecialistName, RiskSpecialistDescription),
            (DirectorCode, DirectorName, DirectorDescription),
            (OwnerCode, OwnerName, OwnerDescription),
            (EmployeeCode, EmployeeName, EmployeeDescription),
        ];
    }

    /// <summary>
    /// Возвращает определения системных прав
    /// </summary>
    /// <returns>Исходные права по стабильным кодам</returns>
    internal static (string Code, string Name)[] GetRights()
    {
        return
        [
            (AdminCode, AdminName),
            (ViewRiskCode, ViewRiskName),
            (CommentRiskCode, CommentRiskName),
            (CommentTreatmentCode, CommentTreatmentName),
            (CreateRiskCode, CreateRiskName),
            (EditRiskCode, EditRiskName),
            (CloseRiskCode, CloseRiskName),
            (AssessRiskCode, AssessRiskName),
            (AcceptOwnerRiskCode, AcceptOwnerRiskName),
            (AcceptDirectorRiskCode, AcceptDirectorRiskName),
            (AcceptCeoRiskCode, AcceptCeoRiskName),
            (CreateTreatmentCode, CreateTreatmentName),
            (EditTreatmentCode, EditTreatmentName),
            (AssignTreatmentExecutorCode, AssignTreatmentExecutorName),
            (ExecuteTreatmentCode, ExecuteTreatmentName),
            (ApproveOwnerTreatmentCode, ApproveOwnerTreatmentName),
            (ApproveDirectorTreatmentCode, ApproveDirectorTreatmentName),
            (ApproveCeoTreatmentCode, ApproveCeoTreatmentName),
            (ViewReportCode, ViewReportName),
            (ExportReportCode, ExportReportName),
            (ImportDataCode, ImportDataName),
            (SplitRiskCode, SplitRiskName),
            (TransferRiskCode, TransferRiskName),
        ];
    }
}
