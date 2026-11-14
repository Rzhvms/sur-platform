using Infrastructure.Repositories.Risks.Helpers;
using System.Data;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;

namespace Infrastructure.Repositories.Risks;

/// <summary>Распространяет замену владельца на действующие связи риска в транзакции оргструктуры</summary>
internal sealed partial class RiskRepository
{
    /// <summary>Формат системного комментария о подтверждённой замене</summary>
    private const string OwnerChangedFormat = "Изменён владелец процесса «{0}»: {1} → {2}";

    /// <summary>Обновляет все текущие связи, версии рисков и системные комментарии</summary>
    /// <param name="transaction">Транзакция изменения процесса</param>
    /// <param name="actor">Инициатор</param>
    /// <param name="before">Процесс до изменения</param>
    /// <param name="after">Процесс после изменения</param>
    public async Task UpdateProcessOwnershipAsync(IDbTransaction transaction, Guid actor, ProcessDal before, ProcessDal after)
    {
        var db = transaction.Connection ?? throw new InvalidOperationException();
        var processId = after.Id;
        var ownerId = after.OwnerId;
        var operationId = Guid.NewGuid();
        await BusinessHistory.RecordAsync(transaction, operationId, actor, processId, before, after);

        var selectQuery = $"""
            SELECT * FROM {EntityMapper.TbName<RiskProcessDal>()} WHERE {EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)} = {EntityMapper.ParameterName(nameof(processId))} AND {EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL
            ORDER BY {EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)}
            """;
        var previousLinks = (await db.QueryAsync<RiskProcessDal>(selectQuery, new { processId }, transaction)).ToArray();
        if (previousLinks.Length == 0)
        {
            return;
        }

        var riskIds = previousLinks.Select(link => link.RiskId).Distinct().ToArray();
        var readRisksQuery = $"SELECT * FROM {EntityMapper.TbName<RiskDal>()} WHERE {EntityMapper.ColName<RiskDal>(x => x.Id)} = ANY({EntityMapper.ParameterName(nameof(riskIds))})";
        var previousRisks = (await db.QueryAsync<RiskDal>(readRisksQuery, new { riskIds }, transaction)).ToDictionary(risk => risk.Id);

        var updateLinksQuery = $"""
            UPDATE {EntityMapper.TbName<RiskProcessDal>()} SET {EntityMapper.ColName<RiskProcessDal>(x => x.OwnerId)} = {EntityMapper.ParameterName(nameof(ownerId))}, {EntityMapper.ColName<RiskProcessDal>(x => x.Version)} = gen_random_uuid()
            WHERE {EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)} = {EntityMapper.ParameterName(nameof(processId))} AND {EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL RETURNING *
            """;
        var links = (await db.QueryAsync<RiskProcessDal>(updateLinksQuery, new { processId, ownerId }, transaction))
            .ToDictionary(link => link.RiskId);
        var oldLinks = previousLinks.ToDictionary(link => link.RiskId);

        var updateRisksQuery = $"""
            UPDATE {EntityMapper.TbName<RiskDal>()} SET {EntityMapper.ColName<RiskDal>(x => x.Version)} = gen_random_uuid(), {EntityMapper.ColName<RiskDal>(x => x.UpdatedAt)} = clock_timestamp()
            WHERE {EntityMapper.ColName<RiskDal>(x => x.Id)} = ANY({EntityMapper.ParameterName(nameof(riskIds))}) RETURNING *
            """;
        var risks = await db.QueryAsync<RiskDal>(updateRisksQuery, new { riskIds }, transaction);
        var previousOwner = await ReadOwnerNameAsync(db, transaction, before.OwnerId);
        var nextOwner = await ReadOwnerNameAsync(db, transaction, ownerId);
        var comment = string.Format(System.Globalization.CultureInfo.InvariantCulture, OwnerChangedFormat,
            after.Name, previousOwner, nextOwner);
        foreach (var risk in risks)
        {
            await BusinessHistory.RecordAsync(transaction, operationId, actor, risk.Id, previousRisks[risk.Id], risk,
                relatedPath: BusinessHistory.ProcessesPath, relatedBefore: oldLinks[risk.Id], relatedAfter: links[risk.Id], system: true);
            await InsertCommentAsync(transaction, operationId, risk.Id, actor, comment, true);
        }
    }

    /// <summary>Считывает имя владельца для неизменяемого текста системного комментария</summary>
    /// <param name="db">Клиентская база</param>
    /// <param name="transaction">Транзакция замены</param>
    /// <param name="id">Сотрудник</param>
    /// <returns>Имя либо идентификатор при отсутствии имени</returns>
    private static async Task<string> ReadOwnerNameAsync(IDbConnection db, IDbTransaction transaction, Guid id)
    {
        var query = $"""
            SELECT u.{EntityMapper.ColName<UserProfileDal>(x => x.FullName)} FROM {EntityMapper.TbName<OrgPersonDal>()} p
            JOIN {EntityMapper.TbName<UserProfileDal>()} u ON u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = p.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)}
            WHERE p.{EntityMapper.ColName<OrgPersonDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))}
            """;
        return await db.QuerySingleOrDefaultAsync<string>(query, new { id }, transaction) ?? id.ToString();
    }
}
