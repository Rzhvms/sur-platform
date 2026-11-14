using System.Data;
using Infrastructure.Clients;
using Infrastructure.Services.Jira;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.Ports.Repositories;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Repositories.Identity;
using Infrastructure.Repositories.Sessions.Interfaces;
using Infrastructure.Repositories.Risks.Interfaces;

namespace Infrastructure.Repositories.Risks;

/// <summary>Клиентский реестр рисков и связанные команды</summary>
/// <param name="connection">Подключение выбранной базы</param>
/// <param name="jiraRest">HTTPS-клиент Jira</param>
/// <param name="jiraConfiguration">Список разрешённых адресов</param>
/// <param name="protection">Провайдер защиты реквизитов</param>
/// <param name="databaseContext">Клиентский контекст шифрования</param>
/// <param name="sessions">Проверки действующей сессии и эффективных прав</param>
internal sealed partial class RiskRepository(IDbConnection connection, ITransactionalSessionRepository sessions,
    JiraRestClient jiraRest, IConfiguration jiraConfiguration, IDataProtectionProvider protection, ClientDatabaseContext databaseContext)
    : IRiskRepository, IRiskOrganizationRepository, IReviewRepository, ITransactionalReviewWorkerRepository, IJiraRepository, IJiraWorkerRepository
{
    /// <summary>Выполняет команду под общей блокировкой с оргструктурой и Identity</summary>
    /// <param name="actor">Инициатор из JWT</param>
    /// <param name="right">Обязательное право</param>
    /// <param name="action">Прикладные изменения и запись поручения</param>
    /// <returns>Результат после коммита</returns>
    private async Task<T> WriteAsync<T>(RiskActor actor, string? right, Func<IDbTransaction, Task<T>> action)
    {
        using var transaction = connection.BeginTransaction();
        await AdministrationTransaction.LockAsync(connection, transaction);
        if (right is not null)
        {
            await RequireRightAsync(actor, right, transaction);
        }
        var result = await action(transaction);
        transaction.Commit();
        return result;
    }

    /// <summary>Проверяет право в актуальном состоянии клиентской базы</summary>
    /// <param name="actor">Инициатор из JWT</param>
    /// <param name="right">Код права</param>
    /// <param name="transaction">Транзакция чтения или изменения</param>
    private async Task RequireRightAsync(RiskActor actor, string right, IDbTransaction transaction)
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
    private Task<bool> HasRightAsync(RiskActor actor, string right, IDbTransaction transaction)
    {
        return sessions.IsActiveAsync(actor.UserId, actor.SessionId, DateTime.UtcNow, CancellationToken.None,
            actor.AccessVersion, right, transaction);
    }

    /// <summary>Читает запись по идентификатору</summary>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="idColumn">Имя колонки из EntityMapper</param>
    /// <param name="transaction">Текущая транзакция</param>
    /// <returns>Строка клиентской базы</returns>
    private async Task<T> ReadAsync<T>(Guid id, string idColumn, IDbTransaction transaction) where T : class
    {
        var query = $"SELECT * FROM {EntityMapper.TbName<T>()} WHERE {idColumn} = {EntityMapper.ParameterName(nameof(id))}";
        return await connection.QuerySingleOrDefaultAsync<T>(query, new { id }, transaction)
            ?? throw new IdentityCommandException(404, RiskRules.NotFound);
    }

    /// <summary>Проверяет обязательный текст</summary>
    /// <param name="value">Текст из контракта</param>
    /// <returns>Текст без крайних пробелов</returns>
    private static string RequiredText(string? value)
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
    private static void RequireVersion(Guid current, Guid? expected)
    {
        if (current != expected)
        {
            throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
        }
    }

    /// <summary>Проверяет организационный контекст владельца основной связи</summary>
    /// <param name="actor">Инициатор</param>
    /// <param name="riskId">Риск с основной связью</param>
    /// <param name="transaction">Текущая транзакция</param>
    private async Task RequirePrimaryOwnerAsync(RiskActor actor, Guid riskId, IDbTransaction transaction)
    {
        if (await HasRightAsync(actor, IdentityRights.AdminCode, transaction))
        {
            return;
        }

        var userId = actor.UserId;
        var query = $"""
            SELECT EXISTS (
                SELECT FROM {EntityMapper.TbName<RiskProcessDal>()} link
                JOIN {EntityMapper.TbName<OrgPersonDal>()} person
                  ON person.{EntityMapper.ColName<OrgPersonDal>(x => x.Id)} = link.{EntityMapper.ColName<RiskProcessDal>(x => x.OwnerId)}
                WHERE link.{EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(riskId))}
                  AND link.{EntityMapper.ColName<RiskProcessDal>(x => x.IsPrimary)}
                  AND link.{EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL
                  AND person.{EntityMapper.ColName<OrgPersonDal>(x => x.IsActive)}
                  AND person.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)} = {EntityMapper.ParameterName(nameof(userId))}
            )
            """;
        if (!await connection.ExecuteScalarAsync<bool>(query, new { riskId, userId }, transaction))
        {
            throw new IdentityCommandException(403, IdentityErrorCodes.AccessDenied);
        }
    }
}
