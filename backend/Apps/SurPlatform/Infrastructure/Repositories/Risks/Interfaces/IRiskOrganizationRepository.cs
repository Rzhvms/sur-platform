using System.Data;
using Domain.DalModels;

namespace Infrastructure.Repositories.Risks.Interfaces;

/// <summary>Применяет последствия изменений оргструктуры в транзакции исходной команды</summary>
internal interface IRiskOrganizationRepository
{
    /// <summary>Обновляет связи, версии рисков и системные комментарии</summary>
    /// <param name="transaction">Транзакция изменения процесса</param>
    /// <param name="actor">Инициатор</param>
    /// <param name="before">Процесс до изменения</param>
    /// <param name="after">Процесс после изменения</param>
    Task UpdateProcessOwnershipAsync(IDbTransaction transaction, Guid actor, ProcessDal before, ProcessDal after);

    /// <summary>Пересчитывает оценки рисков при изменении ставки простоя</summary>
    /// <param name="transaction">Транзакция изменения объекта</param>
    /// <param name="actor">Инициатор</param>
    /// <param name="operationId">Идентификатор операции для журнала</param>
    /// <param name="asset">Объект с новой ставкой</param>
    Task RecalculateAssetAsync(IDbTransaction transaction, Guid actor, Guid operationId, AssetDal asset);
}
