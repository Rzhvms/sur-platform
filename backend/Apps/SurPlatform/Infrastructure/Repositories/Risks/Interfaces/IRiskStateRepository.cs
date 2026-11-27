using System.Data;
using Application.Models;
using Application.UseCases.Risks.Dto;
using Domain.DalModels;

namespace Infrastructure.Repositories.Risks.Interfaces;

/// <summary>Паспорт и организационные связи риска в транзакции вызывающего сценария</summary>
internal interface IRiskStateRepository
{
    /// <summary>Проверяет, что процессы и их владельцы действуют</summary>
    /// <param name="processIds">Выбранные процессы</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <returns>Действующие процессы</returns>
    Task<ProcessDal[]> ReadActiveProcessesAsync(Guid[] processIds, IDbTransaction transaction);

    /// <summary>Читает действующие связи риска</summary>
    /// <param name="id">Риск</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <returns>Действующие связи</returns>
    Task<RiskProcessDal[]> ReadActiveRiskLinksAsync(Guid id, IDbTransaction transaction);

    /// <summary>Создаёт действующую связь с текущим владельцем процесса</summary>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <param name="id">Риск</param>
    /// <param name="process">Действующий процесс</param>
    /// <param name="isPrimary">Признак основной связи</param>
    /// <returns>Созданная связь</returns>
    Task<RiskProcessDal> InsertProcessLinkAsync(IDbTransaction transaction, Guid id, ProcessDal process, bool isPrimary);

    /// <summary>Меняет основную связь или закрывает исключённую связь</summary>
    /// <param name="transaction">Транзакция разделения</param>
    /// <param name="link">Ожидаемая действующая связь</param>
    /// <param name="primary">Признак основного процесса</param>
    /// <param name="keep">Сохраняется ли связь в новом наборе</param>
    /// <returns>Новая версия связи</returns>
    Task<RiskProcessDal> SaveProcessLinkAsync(IDbTransaction transaction, RiskProcessDal link, bool primary, bool keep);

    /// <summary>Регистрирует риск и историю в транзакции вызывающей команды</summary>
    /// <param name="actor">Инициатор регистрации</param>
    /// <param name="request">Паспорт нового риска</param>
    /// <param name="transaction">Транзакция со всеми связанными изменениями</param>
    /// <returns>Созданный черновик</returns>
    Task<RiskDal> CreateRiskAsync(RiskActor actor, RiskPassportRequest request, IDbTransaction transaction);

    /// <summary>Проверяет доступность новых связей и сохраняет существующие исторические ссылки</summary>
    /// <param name="request">Запрошенный паспорт</param>
    /// <param name="previous">Предыдущий паспорт при редактировании</param>
    /// <param name="transaction">Транзакция команды</param>
    Task ValidatePassportAsync(RiskPassportRequest request, RiskDal? previous, IDbTransaction transaction);

    /// <summary>Фиксирует весь пакет технического пересчёта или изменения маршрутов</summary>
    /// <param name="transaction">Транзакция настройки или объекта</param>
    /// <param name="actor">Инициатор изменения</param>
    /// <param name="operationId">Общий идентификатор журнала</param>
    /// <param name="scale">Новая шкала для технических версий</param>
    /// <param name="settings">Пороги принятия</param>
    /// <param name="asset">Объект с новой стоимостью</param>
    Task RecalculateAsync(IDbTransaction transaction, Guid actor, Guid operationId,
        ImpactScale? scale, RiskAcceptanceSettingsDal settings, AssetDal? asset = null);

    /// <summary>Проверяет организационный контекст владельца основной связи</summary>
    /// <param name="actor">Инициатор</param>
    /// <param name="riskId">Риск с основной связью</param>
    /// <param name="transaction">Текущая транзакция</param>
    Task RequirePrimaryOwnerAsync(RiskActor actor, Guid riskId, IDbTransaction transaction);
}
