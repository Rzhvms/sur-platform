using System.Data;
using Application.Models;
using Domain.DalModels;

namespace Infrastructure.Repositories.Assessments.Interfaces;

/// <summary>Версии оценки и состояния риска в транзакции вызывающего сценария</summary>
internal interface IRiskAssessmentStateRepository
{
    /// <summary>Читает единственную текущую оценку риска</summary>
    /// <param name="id">Риск</param>
    /// <param name="transaction">Транзакция операции</param>
    /// <returns>Текущая запись при наличии</returns>
    Task<RiskAssessmentDal?> ReadCurrentAssessmentAsync(Guid id, IDbTransaction transaction);

    /// <summary>Сохраняет неизменяемые входы и результаты новой оценки</summary>
    /// <param name="transaction">Транзакция изменения риска</param>
    /// <param name="assessment">Рассчитанное состояние</param>
    /// <param name="author">Автор версии</param>
    /// <returns>Созданная версия</returns>
    Task<RiskAssessmentDal> InsertAssessmentAsync(IDbTransaction transaction, RiskAssessmentDal assessment, Guid author);

    /// <summary>Меняет служебные поля версии оценки</summary>
    /// <param name="transaction">Транзакция команды</param>
    /// <param name="assessment">Ожидаемая версия оценки</param>
    /// <param name="active">Актуальность версии</param>
    /// <param name="mode">Режим текущего цикла</param>
    /// <returns>Обновлённые метаданные оценки</returns>
    Task<RiskAssessmentDal> SetAssessmentStateAsync(IDbTransaction transaction, RiskAssessmentDal assessment,
        bool active, string? mode);

    /// <summary>Обновляет жизненный цикл, маршрут и конкурентную версию риска</summary>
    /// <param name="transaction">Транзакция изменения</param>
    /// <param name="risk">Прежняя карточка</param>
    /// <param name="status">Новый статус</param>
    /// <param name="level">Актуальный маршрут принятия</param>
    /// <param name="clearCloseReason">Признак очистки причины прежнего закрытия</param>
    /// <returns>Карточка после записи</returns>
    Task<RiskDal> SaveRiskStateAsync(IDbTransaction transaction, RiskDal risk, string status, string? level, bool clearCloseReason = false);

    /// <summary>Читает действующие пороги встроенного метода</summary>
    /// <param name="transaction">Транзакция операции</param>
    /// <returns>Текущие пороги</returns>
    Task<RiskAcceptanceSettingsDal> ReadAcceptanceSettingsAsync(IDbTransaction transaction);

    /// <summary>Возвращает опубликованную шкалу по идентификатору либо последнюю</summary>
    /// <param name="transaction">Транзакция операции</param>
    /// <param name="id">Запрошенная историческая версия</param>
    /// <returns>Шкала при наличии</returns>
    Task<ImpactScale?> ReadScaleAsync(IDbTransaction transaction, Guid? id = null);
}
