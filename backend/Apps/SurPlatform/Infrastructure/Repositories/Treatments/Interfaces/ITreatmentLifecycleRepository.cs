using System.Data;
using Domain.DalModels;
using Domain.DalModels.Treatments;

namespace Infrastructure.Repositories.Treatments.Interfaces;

/// <summary>Завершение обработки риска в транзакции изменения меры</summary>
internal interface ITreatmentLifecycleRepository
{
    /// <summary>Открывает пересмотр после последней активной меры</summary>
    /// <param name="measure">Исходное состояние меры</param>
    /// <param name="confirmed">Подтверждение при оставшихся черновиках</param>
    /// <param name="transaction">Транзакция перехода</param>
    /// <param name="actor">Пользователь исходной команды</param>
    /// <param name="operationId">Общее событие меры и риска</param>
    /// <param name="jira">Настройка внешней операции</param>
    Task StartReviewAfterTreatmentAsync(TreatmentDal measure, bool confirmed, Guid actor, Guid operationId,
        IDbTransaction transaction, JiraSettingsDal? jira = null);
}
