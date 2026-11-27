using System.Data;
using Application.Models;
using Domain.DalModels;
using Domain.DalModels.Risks;
using Domain.DalModels.Treatments;

namespace Infrastructure.Repositories.Treatments.Interfaces;

/// <summary>Проверки и изменение меры в транзакции вызывающего сценария</summary>
internal interface ITreatmentStateRepository
{
    /// <summary>Загружает текущее назначение меры</summary>
    /// <param name="id">Мера</param>
    /// <param name="transaction">Транзакция операции</param>
    /// <returns>Назначение, если есть</returns>
    Task<TreatmentAssignmentDal?> ReadAssignmentAsync(Guid id, IDbTransaction transaction);

    /// <summary>Проверяет наличие пригодного исполнителя при входе в работу</summary>
    /// <param name="measure">Мера перед запуском или возобновлением</param>
    /// <param name="transaction">Транзакция операции</param>
    Task RequireValidAssignmentAsync(TreatmentDal measure, IDbTransaction transaction);

    /// <summary>Отбирает сотрудников дирекции с действующей ролью исполнителя</summary>
    /// <param name="processId">Процесс меры</param>
    /// <param name="personId">Конкретный кандидат, если выбран</param>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <param name="transaction">Транзакция операции</param>
    /// <returns>Допустимые исполнители</returns>
    Task<TreatmentExecutorOption[]> ReadEligibleExecutorsAsync(Guid processId, Guid? personId,
        int offset, int limit, IDbTransaction transaction);

    /// <summary>Меняет статус меры, проверяет слот и ставит поручение журнала</summary>
    /// <param name="measure">Мера и ожидаемая версия</param>
    /// <param name="status">Целевой статус</param>
    /// <param name="actor">Инициатор действия</param>
    /// <param name="operationId">Общая операция журнала</param>
    /// <param name="transaction">Транзакция риск–мера</param>
    /// <param name="cost">Фактическая стоимость при завершении</param>
    /// <param name="jira">Атрибуция внешней операции</param>
    /// <returns>Обновлённая мера</returns>
    Task<TreatmentDal> SaveTreatmentStatusAsync(TreatmentDal measure, string status, Guid actor, Guid operationId,
        IDbTransaction transaction, decimal? cost = null, JiraSettingsDal? jira = null);

    /// <summary>Запрещает решение по риску или удаление связи при блокирующих мерах</summary>
    /// <param name="transaction">Транзакция решения</param>
    /// <param name="riskId">Риск</param>
    /// <param name="excludedProcesses">Исключаемые процессы, null для принятия или закрытия</param>
    /// <param name="includeDrafts">Запретить и черновые меры при передаче</param>
    Task RequireNoBlockingTreatmentsAsync(IDbTransaction transaction, Guid riskId, Guid[]? excludedProcesses = null, bool includeDrafts = false);

    /// <summary>Выбирает текущую связь строго из того же риска</summary>
    /// <param name="riskId">Риск</param>
    /// <param name="linkId">Выбранная связь или null для единственной</param>
    /// <param name="transaction">Транзакция операции</param>
    /// <returns>Подходящая активная связь</returns>
    Task<RiskProcessDal> SelectTreatmentLinkAsync(Guid riskId, Guid? linkId, IDbTransaction transaction);

    /// <summary>Проверяет оценку и режим цикла риска</summary>
    /// <param name="riskId">Риск</param>
    /// <param name="status">Требуемый статус риска</param>
    /// <param name="transaction">Транзакция операции</param>
    /// <returns>Карточка допустимого риска</returns>
    Task<RiskDal> RequireTreatmentCycleAsync(Guid riskId, string status, IDbTransaction transaction);
}
