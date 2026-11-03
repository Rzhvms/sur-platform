using Core.Database.Transactions;
using Dapper;

namespace Core.Database.Repositories;

/// <summary>
/// Общие параметризованные операции Dapper в области единицы работы
/// </summary>
public abstract class DapperRepository(DbUnitOfWork database)
{
    /// <summary>
    /// Читает записи с учётом текущей транзакции
    /// </summary>
    protected async Task<T[]> Query<T>(string sql, object? parameters, CancellationToken cancellationToken)
    {
        await database.OpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, parameters, database.CurrentTransaction, cancellationToken: cancellationToken);
        return (await database.Connection.QueryAsync<T>(command)).ToArray();
    }
    /// <summary>
    /// Читает единственную запись либо возвращает отсутствие результата
    /// </summary>
    protected async Task<T?> One<T>(string sql, object? parameters, CancellationToken cancellationToken)
    {
        await database.OpenAsync(cancellationToken);
        var command = new CommandDefinition(sql, parameters, database.CurrentTransaction, cancellationToken: cancellationToken);
        return await database.Connection.QuerySingleOrDefaultAsync<T>(command);
    }
    /// <summary>
    /// Изменяет данные только внутри явно открытой транзакции
    /// </summary>
    protected Task<int> Execute(string sql, object? parameters, CancellationToken cancellationToken)
    {
        if (database.CurrentTransaction == null)
            throw new InvalidOperationException("Изменения требуют явной транзакции");
        var command = new CommandDefinition(sql, parameters, database.CurrentTransaction, cancellationToken: cancellationToken);
        return database.Connection.ExecuteAsync(command);
    }
}
