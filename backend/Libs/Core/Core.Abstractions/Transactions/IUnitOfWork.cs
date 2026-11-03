namespace Core.Abstractions.Transactions;

/// <summary>
/// Открытие общей транзакции для согласованных изменений хранилищ
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Открывает новую область транзакции
    /// </summary>
    Task<ITransaction> BeginAsync(CancellationToken cancellationToken);
}
