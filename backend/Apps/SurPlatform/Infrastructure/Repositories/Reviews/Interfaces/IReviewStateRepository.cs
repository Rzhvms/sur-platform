using System.Data;
using Application.Models;
using Domain.DalModels;
using Domain.DalModels.Assessments;
using Domain.DalModels.Reviews;
using Domain.DalModels.Risks;

namespace Infrastructure.Repositories.Reviews.Interfaces;

/// <summary>Запуск пересмотра и остановка мер в общей транзакции</summary>
internal interface IReviewStateRepository
{
    /// <summary>Читает хронологию без секретов и исходных тел вебхука</summary>
    /// <param name="id">Риск</param>
    /// <param name="offset">Смещение окна</param>
    /// <param name="limit">Размер окна</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <returns>История результатов со снимками</returns>
    Task<RiskReviewView[]> ReadRiskReviewsAsync(Guid id, int offset, int limit, IDbTransaction transaction);

    /// <summary>Сохраняет запуск с фиксированным источником и набором целей</summary>
    /// <param name="row">Снимок запуска</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    Task InsertReviewRunAsync(ReviewRunDal row, IDbTransaction transaction);

    /// <summary>Открывает пересмотр для одного риска</summary>
    /// <param name="risk">Исходный риск</param>
    /// <param name="run">Сохранённый запуск</param>
    /// <param name="operationId">Связь записей общего журнала</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <param name="relatedChanges">Изменения процессов при передаче</param>
    /// <returns>Карточка в ожидании переоценки</returns>
    Task<RiskDal> OpenReviewAsync(RiskDal risk, ReviewRunDal run, Guid operationId, IDbTransaction transaction,
        List<(string Path, object? Before, object After)>? relatedChanges = null);

    /// <summary>Создаёт и открывает однорисковый пересмотр</summary>
    /// <param name="risk">Исходный риск</param>
    /// <param name="reason">Основание запуска</param>
    /// <param name="source">Тип источника</param>
    /// <param name="sourceId">Идентификатор источника</param>
    /// <param name="actor">Инициатор бизнес-команды</param>
    /// <param name="operationId">Общий идентификатор журнала</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <param name="relatedChanges">Изменения процессов при передаче</param>
    /// <param name="jira">Атрибуция пересмотра из Jira</param>
    /// <returns>Карточка в ожидании переоценки</returns>
    Task<RiskDal> StartSingleReviewAsync(RiskDal risk, string reason, string source, Guid sourceId, Guid actor,
        Guid operationId, IDbTransaction transaction, List<(string Path, object? Before, object After)>? relatedChanges = null,
        JiraSettingsDal? jira = null);

    /// <summary>Фиксирует результат первичной обработки</summary>
    /// <param name="runId">Первоначальный запуск</param>
    /// <param name="riskId">Целевой риск</param>
    /// <param name="status">Результат обработки</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <param name="previous">Последняя оценка</param>
    /// <param name="startedBy">Инициатор начала</param>
    Task InsertRiskReviewAsync(Guid runId, Guid riskId, string status, IDbTransaction transaction,
        Guid? previous = null, Guid? startedBy = null);

    /// <summary>Читает единственный открытый пересмотр</summary>
    /// <param name="riskId">Риск</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <returns>Открытый пересмотр при наличии</returns>
    Task<RiskReviewDal?> ReadWaitingReviewAsync(Guid riskId, IDbTransaction transaction);

    /// <summary>Читает последнюю оценку, включая историческую</summary>
    /// <param name="id">Риск</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <returns>Последняя оценка при наличии</returns>
    Task<RiskAssessmentDal?> ReadLatestAssessmentAsync(Guid id, IDbTransaction transaction);

    /// <summary>Завершает открытый пересмотр без изменения первоначального запуска</summary>
    /// <param name="riskId">Риск</param>
    /// <param name="actor">Исполнитель</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <param name="assessmentId">Новая оценка</param>
    /// <param name="closeReason">Причина закрытия</param>
    Task CompleteReviewAsync(Guid riskId, Guid actor, IDbTransaction transaction, Guid? assessmentId, string? closeReason);

    /// <summary>Проверяет действующего ответственного</summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    Task RequireReviewResponsibleAsync(Guid userId, IDbTransaction transaction);
}
