using System.Data;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Infrastructure.Observability;
using Infrastructure.Repositories.Identity;
using Infrastructure.Repositories.Sessions.Interfaces;

namespace Infrastructure.Repositories.Shared;

/// <summary>Общие транзакционные операции и проверка доступа к клиентской базе</summary>
internal abstract class ClientRepository
{
    /// <summary>Подключение выбранной клиентской базы</summary>
    protected readonly IDbConnection _connection;

    /// <summary>Проверка сессии и эффективных прав в транзакции</summary>
    protected readonly ITransactionalSessionRepository _sessions;

    /// <summary>Метрики операций базы</summary>
    protected readonly OperationsMetrics _operations;

    /// <summary>Инициализирует операции выбранной клиентской базы</summary>
    /// <param name="connection">Подключение выбранной клиентской базы</param>
    /// <param name="sessions">Проверка сессии и эффективных прав в транзакции</param>
    /// <param name="operations">Метрики операций базы</param>
    protected ClientRepository(
        IDbConnection connection,
        ITransactionalSessionRepository sessions,
        OperationsMetrics operations)
    {
        _connection = connection;
        _sessions = sessions;
        _operations = operations;
    }

    /// <summary>Классификатор конфликтов транзакции реестра</summary>
    protected const string PostgresOperation = "risk.postgres";

    /// <summary>Выполняет команду под общей блокировкой с оргструктурой и Identity</summary>
    /// <param name="actor">Инициатор из JWT</param>
    /// <param name="right">Обязательное право</param>
    /// <param name="action">Прикладные изменения и запись поручения</param>
    /// <returns>Результат после коммита</returns>
    protected async Task<T> WriteAsync<T>(RiskActor actor, string? right, Func<IDbTransaction, Task<T>> action)
    {
        using var transaction = _connection.BeginTransaction();
        await AdministrationTransaction.LockAsync(_connection, transaction);
        if (right is not null)
        {
            await RequireRightAsync(actor, right, transaction);
        }
        try
        {
            var result = await action(transaction);
            transaction.Commit();
            return result;
        }
        catch (Npgsql.PostgresException error) when (error.SqlState is Npgsql.PostgresErrorCodes.SerializationFailure or
            Npgsql.PostgresErrorCodes.DeadlockDetected or Npgsql.PostgresErrorCodes.UniqueViolation)
        {
            _operations.Outcome(PostgresOperation, error.SqlState);
            throw;
        }
        catch (IdentityCommandException error) when (error.Code == IdentityErrorCodes.StaleVersion)
        {
            _operations.Outcome(PostgresOperation, IdentityErrorCodes.StaleVersion);
            throw;
        }
    }

    /// <summary>Проверяет право в актуальном состоянии клиентской базы</summary>
    /// <param name="actor">Инициатор из JWT</param>
    /// <param name="right">Код права</param>
    /// <param name="transaction">Транзакция чтения или изменения</param>
    protected async Task RequireRightAsync(RiskActor actor, string right, IDbTransaction transaction)
    {
        if (!await HasRightAsync(actor, right, transaction))
        {
            throw new IdentityCommandException(403, IdentityErrorCodes.AccessDenied);
        }
    }

    /// <summary>Читает актуальное право участника в контексте команды</summary>
    /// <param name="actor">Инициатор из JWT</param>
    /// <param name="right">Код права</param>
    /// <param name="transaction">Текущая транзакция</param>
    /// <returns>Признак эффективного права</returns>
    protected Task<bool> HasRightAsync(RiskActor actor, string right, IDbTransaction transaction)
    {
        return _sessions.IsActiveAsync(actor.UserId, actor.SessionId, DateTime.UtcNow, CancellationToken.None,
            actor.AccessVersion, right, transaction);
    }

    /// <summary>Читает запись по идентификатору</summary>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="idColumn">Имя колонки из EntityMapper</param>
    /// <param name="transaction">Текущая транзакция</param>
    /// <returns>Строка клиентской базы</returns>
    protected async Task<T> ReadAsync<T>(Guid id, string idColumn, IDbTransaction transaction) where T : class
    {
        var query = $"SELECT * FROM {EntityMapper.TbName<T>()} WHERE {idColumn} = {EntityMapper.ParameterName(nameof(id))}";
        return await _connection.QuerySingleOrDefaultAsync<T>(query, new { id }, transaction)
            ?? throw new IdentityCommandException(404, RiskRules.NotFound);
    }

    /// <summary>Проверяет обязательный текст</summary>
    /// <param name="value">Текст из контракта</param>
    /// <returns>Текст без крайних пробелов</returns>
    protected static string RequiredText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new IdentityCommandException(400, RiskRules.Invalid);
        }

        return value.Trim();
    }

    /// <summary>Проверяет актуальность версии редактируемой строки</summary>
    /// <param name="current">Текущая версия</param>
    /// <param name="expected">Версия из формы</param>
    protected static void RequireVersion(Guid current, Guid? expected)
    {
        if (current != expected)
        {
            throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
        }
    }
}
