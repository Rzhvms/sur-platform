using System.Data;

namespace Infrastructure.Repositories.Sessions.Interfaces;

/// <summary>Проверяет доступ в транзакции клиентской операции</summary>
internal interface ITransactionalSessionRepository
{
    /// <summary>Проверяет активность сессии и актуальность права</summary>
    /// <param name="userId">Пользователь из токена</param>
    /// <param name="sessionId">Сессия из токена</param>
    /// <param name="now">Текущее время UTC</param>
    /// <param name="cancellationToken">Отмена проверки</param>
    /// <param name="accessVersion">Ожидаемая версия доступа</param>
    /// <param name="rightCode">Требуемый код права</param>
    /// <param name="transaction">Транзакция вызывающей операции</param>
    /// <returns>Разрешён ли доступ</returns>
    Task<bool> IsActiveAsync(Guid userId, Guid sessionId, DateTime now, CancellationToken cancellationToken,
        long accessVersion, string rightCode, IDbTransaction transaction);
}
