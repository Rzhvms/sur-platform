using System.Data;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;

namespace Infrastructure.Repositories.Risks;

/// <summary>Допустимые процессы и объекты для нового риска</summary>
internal sealed partial class RiskRepository
{
    /// <inheritdoc/>
    public async Task<RiskProcessOption[]> ListProcessesAsync(RiskActor actor, int offset, int limit)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.Create, transaction);
        var userId = actor.UserId;
        var allDirectorates = await HasRightAsync(actor, IdentityRights.AdminCode, transaction);
        var result = await ListProcessOptionsAsync(transaction, userId, allDirectorates, offset, limit);
        transaction.Commit();
        return result;
    }

    /// <inheritdoc/>
    public async Task<RiskProcessOption[]> ListSplitProcessesAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.Split, transaction);
        var risk = await ReadAsync<RiskDal>(id, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
        await RequirePrimaryOwnerAsync(actor, id, transaction);
        if (risk.Status != RiskRules.Active || await ReadCurrentAssessmentAsync(id, transaction) is not { ResponseMode: null })
        {
            throw new IdentityCommandException(409, RiskRules.InvalidTransition);
        }
        var result = await ListProcessOptionsAsync(transaction, actor.UserId, true, offset, limit);
        transaction.Commit();
        return result;
    }

    /// <inheritdoc/>
    public async Task<RiskProcessOption[]> ListTransferProcessesAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, ReviewRules.TransferRight, transaction);
        var risk = await ReadAsync<RiskDal>(id, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
        await RequirePrimaryOwnerAsync(actor, id, transaction);
        if (risk.Status != RiskRules.Active || await ReadCurrentAssessmentAsync(id, transaction) is not { ResponseMode: null })
        {
            throw new IdentityCommandException(409, RiskRules.InvalidTransition);
        }
        await RequireNoBlockingTreatmentsAsync(transaction, id, includeDrafts: true);
        var currentProcesses = (await ReadActiveRiskLinksAsync(id, transaction)).Select(link => link.ProcessId).ToArray();
        var result = await ListProcessOptionsAsync(transaction, actor.UserId, true, offset, limit, currentProcesses);
        transaction.Commit();
        return result;
    }

    /// <summary>Читает действующие процессы для выбора в форме</summary>
    /// <param name="transaction">Согласованный снимок</param>
    /// <param name="userId">Пользователь формы</param>
    /// <param name="allDirectorates">Доступны процессы всех дирекций</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <param name="excluded">Процессы, уже связанные с передаваемым риском</param>
    /// <returns>Допустимые процессы и их владельцы</returns>
    private async Task<RiskProcessOption[]> ListProcessOptionsAsync(IDbTransaction transaction, Guid userId,
        bool allDirectorates, int offset, int limit, Guid[]? excluded = null)
    {
        var query = $"""
            SELECT p.{EntityMapper.ColName<ProcessDal>(x => x.Id)}, p.{EntityMapper.ColName<ProcessDal>(x => x.Name)}, p.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)}, p.{EntityMapper.ColName<ProcessDal>(x => x.OwnerId)},
                u.{EntityMapper.ColName<UserProfileDal>(x => x.FullName)} AS {EntityMapper.ColName<RiskProcessOption>(x => x.OwnerName)}
            FROM {EntityMapper.TbName<ProcessDal>()} p
            JOIN {EntityMapper.TbName<DirectorateDal>()} d ON d.{EntityMapper.ColName<DirectorateDal>(x => x.Id)} = p.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)}
            JOIN {EntityMapper.TbName<OrgPersonDal>()} o ON o.{EntityMapper.ColName<OrgPersonDal>(x => x.Id)} = p.{EntityMapper.ColName<ProcessDal>(x => x.OwnerId)}
            JOIN {EntityMapper.TbName<UserProfileDal>()} u ON u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = o.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)}
            WHERE p.{EntityMapper.ColName<ProcessDal>(x => x.IsActive)} AND d.{EntityMapper.ColName<DirectorateDal>(x => x.IsActive)}
              AND o.{EntityMapper.ColName<OrgPersonDal>(x => x.IsActive)} AND u.{EntityMapper.ColName<UserProfileDal>(x => x.IsActive)}
              AND (CAST({EntityMapper.ParameterName(nameof(excluded))} AS uuid[]) IS NULL
                  OR NOT (p.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = ANY({EntityMapper.ParameterName(nameof(excluded))})))
              AND ({EntityMapper.ParameterName(nameof(allDirectorates))} OR EXISTS (SELECT FROM {EntityMapper.TbName<OrgPersonDal>()} member
                  WHERE member.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)} = {EntityMapper.ParameterName(nameof(userId))} AND member.{EntityMapper.ColName<OrgPersonDal>(x => x.IsActive)}
                    AND member.{EntityMapper.ColName<OrgPersonDal>(x => x.DirectorateId)} = p.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)}))
            ORDER BY p.{EntityMapper.ColName<ProcessDal>(x => x.Name)}, p.{EntityMapper.ColName<ProcessDal>(x => x.Id)} OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var result = await connection.QueryAsync<RiskProcessOption>(query, new { allDirectorates, userId, offset, limit, excluded }, transaction);
        return result.ToArray();
    }

    /// <inheritdoc/>
    public async Task<AssetDal[]> ListAssetsAsync(RiskActor actor, int offset, int limit)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        if (!await HasRightAsync(actor, RiskRules.Create, transaction) &&
            !await HasRightAsync(actor, RiskRules.Edit, transaction))
        {
            throw new IdentityCommandException(403, IdentityErrorCodes.AccessDenied);
        }
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<AssetDal>()} WHERE {EntityMapper.ColName<AssetDal>(x => x.IsActive)}
              AND {EntityMapper.ColName<AssetDal>(x => x.HourlyDowntimeCost)} >= 0 AND {EntityMapper.ColName<AssetDal>(x => x.HourlyDowntimeCost)} = round({EntityMapper.ColName<AssetDal>(x => x.HourlyDowntimeCost)}, 2)
            ORDER BY {EntityMapper.ColName<AssetDal>(x => x.Name)}, {EntityMapper.ColName<AssetDal>(x => x.Id)} OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var result = await connection.QueryAsync<AssetDal>(query, new { offset, limit }, transaction);
        transaction.Commit();
        return result.ToArray();
    }
}
