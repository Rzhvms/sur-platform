using System.Data;
using System.Data.Common;
using Core.Abstractions.Transactions;

namespace Core.Database.Transactions;

/// <summary>
/// Управление транзакцией общего подключения ADO.NET
/// </summary>
public sealed class DbUnitOfWork(DbConnection connection, IsolationLevel isolationLevel = IsolationLevel.Serializable) : IUnitOfWork, IAsyncDisposable
{
    /// <summary>
    /// Подключение, используемое хранилищами текущей области зависимостей
    /// </summary>
    public DbConnection Connection { get; } = connection;
    /// <summary>
    /// Текущая транзакция либо отсутствие открытой транзакции
    /// </summary>
    public DbTransaction? CurrentTransaction { get; private set; }
    /// <summary>
    /// Открывает подключение при необходимости
    /// </summary>
    public async Task OpenAsync(CancellationToken cancellationToken)
    {
        if (Connection.State != ConnectionState.Open)
            await Connection.OpenAsync(cancellationToken);
    }
    /// <inheritdoc/>
    public async Task<ITransaction> BeginAsync(CancellationToken cancellationToken)
    {
        if (CurrentTransaction != null)
            throw new InvalidOperationException("Транзакция уже открыта");
        await OpenAsync(cancellationToken);
        var transaction = await Connection.BeginTransactionAsync(isolationLevel, cancellationToken);
        CurrentTransaction = transaction;
        return new DbTransactionScope(this, transaction);
    }
    /// <summary>
    /// Освобождает ссылку на завершённую транзакцию
    /// </summary>
    internal void ReleaseTransaction() => CurrentTransaction = null;
    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (CurrentTransaction != null)
            await CurrentTransaction.DisposeAsync();
        CurrentTransaction = null;
    }
}
