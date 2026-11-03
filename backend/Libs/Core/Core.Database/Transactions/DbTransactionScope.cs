using System.Data.Common;
using Core.Abstractions.Transactions;

namespace Core.Database.Transactions;

/// <summary>
/// Фиксация и освобождение транзакции ADO.NET
/// </summary>
internal sealed class DbTransactionScope(DbUnitOfWork unitOfWork, DbTransaction transaction) : ITransaction
{
    /// <inheritdoc/>
    public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await transaction.DisposeAsync();
        }
        finally
        {
            unitOfWork.ReleaseTransaction();
        }
    }
}
