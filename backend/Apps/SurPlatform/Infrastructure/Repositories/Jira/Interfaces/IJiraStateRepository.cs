using System.Data;
using Application.Models;
using Domain.DalModels;
using Domain.DalModels.Jira;
using Domain.DalModels.Treatments;

namespace Infrastructure.Repositories.Jira.Interfaces;

/// <summary>Поручения Jira и настройки в транзакции предметной операции</summary>
internal interface IJiraStateRepository
{
    /// <summary>Читает доставку меры</summary>
    /// <param name="id">Мера</param>
    /// <param name="transaction">Снимок</param>
    /// <returns>Текущая доставка</returns>
    Task<JiraDeliveryDal?> ReadJiraDeliveryAsync(Guid id, IDbTransaction transaction);

    /// <summary>Добавляет задачу в очередь транзакции меры</summary>
    /// <param name="id">Мера</param>
    /// <param name="actor">Инициатор</param>
    /// <param name="transaction">Запуск или возобновление</param>
    /// <param name="triggeredFromRisk">Истина для запуска набора, ложь для возобновления меры</param>
    Task QueueJiraAsync(Guid id, Guid actor, IDbTransaction transaction, bool triggeredFromRisk);

    /// <summary>Определяет доступность ручного управления</summary>
    /// <param name="measure">Мера</param>
    /// <param name="transaction">Транзакция чтения</param>
    /// <param name="viewer">Пользователь карточки, если требуется персональное уведомление</param>
    /// <returns>Данные интеграции</returns>
    Task<JiraTreatmentView> ReadJiraViewAsync(TreatmentDal measure, IDbTransaction transaction, Guid? viewer = null);

    /// <summary>Проверяет активную задержку отправки для инициатора запуска риска</summary>
    /// <param name="riskId">Открытый риск</param>
    /// <param name="viewer">Текущий пользователь</param>
    /// <param name="transaction">Тот же снимок, что и у карточки</param>
    /// <returns>Инициатору следует показать уведомление</returns>
    Task<bool> HasRiskJiraRetryAsync(Guid riskId, Guid viewer, IDbTransaction transaction);

    /// <summary>Запрещает ручную запись при внешнем управлении</summary>
    /// <param name="measure">Мера</param>
    /// <param name="transaction">Транзакция команды</param>
    Task RequireJiraManualControlAsync(TreatmentDal measure, IDbTransaction transaction);

    /// <summary>Читает настройку выбранной клиентской базы</summary>
    /// <param name="transaction">Снимок записи</param>
    /// <returns>Настройка, если создана</returns>
    Task<JiraSettingsDal?> ReadJiraAsync(IDbTransaction? transaction = null);
}
