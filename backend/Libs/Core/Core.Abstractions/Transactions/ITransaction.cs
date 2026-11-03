namespace Core.Abstractions.Transactions;

/// <summary>
/// Область транзакции с явной фиксацией и откатом при освобождении
/// </summary>
public interface ITransaction : IAsyncDisposable
{
    /// <summary>
    /// Фиксирует изменения текущей транзакции
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken);
}
