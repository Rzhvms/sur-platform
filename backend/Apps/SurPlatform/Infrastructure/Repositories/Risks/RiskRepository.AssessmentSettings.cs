using System.Data;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.UseCases.Risks.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Repositories.Risks.Helpers;

namespace Infrastructure.Repositories.Risks;

/// <summary>Публикация денежных шкал и настройка маршрутов принятия</summary>
internal sealed partial class RiskRepository
{
    /// <inheritdoc/>
    public async Task<ImpactScale?> GetImpactScaleAsync(RiskActor actor, Guid? id, bool admin)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        if (admin)
        {
            await RequireRightAsync(actor, IdentityRights.AdminCode, transaction);
        }
        else if (!await HasRightAsync(actor, RiskRules.View, transaction) && !await HasRightAsync(actor, RiskRules.Assess, transaction))
        {
            throw new IdentityCommandException(403, IdentityErrorCodes.AccessDenied);
        }
        var scale = await ReadScaleAsync(transaction, id);
        if (id.HasValue && scale is null)
        {
            throw new IdentityCommandException(404, RiskRules.NotFound);
        }
        transaction.Commit();
        return scale;
    }

    /// <inheritdoc/>
    public Task<ImpactScale> SaveImpactScaleAsync(RiskActor actor, ImpactScaleRequest request)
    {
        return WriteAsync(actor, IdentityRights.AdminCode, async transaction =>
        {
            var previous = await ReadScaleAsync(transaction);
            if (previous?.Version.Id != request.Version)
            {
                throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
            }
            var id = Guid.NewGuid();
            var thresholds = AssessmentCalculation.BuildThresholds(id, request.Minimums);
            if (previous is not null && previous.Thresholds.Select(x => x.MinLossAmount)
                .SequenceEqual(thresholds.Select(x => x.MinLossAmount)))
            {
                return previous;
            }

            var row = new ImpactScaleDal { Id = id, CreatedById = actor.UserId };
            var query = $"""
                INSERT INTO {EntityMapper.TbName<ImpactScaleDal>()}
                    ({EntityMapper.ColName<ImpactScaleDal>(x => x.Id)}, {EntityMapper.ColName<ImpactScaleDal>(x => x.CreatedById)})
                VALUES ({EntityMapper.ParameterName(nameof(row.Id))}, {EntityMapper.ParameterName(nameof(row.CreatedById))}) RETURNING *
                """;
            var scale = new ImpactScale(await connection.QuerySingleAsync<ImpactScaleDal>(query, row, transaction), thresholds);
            var thresholdQuery = $"""
                INSERT INTO {EntityMapper.TbName<ImpactThresholdDal>()}
                    ({EntityMapper.ColName<ImpactThresholdDal>(x => x.ScaleId)}, {EntityMapper.ColName<ImpactThresholdDal>(x => x.Weight)}, {EntityMapper.ColName<ImpactThresholdDal>(x => x.MinLossAmount)}, {EntityMapper.ColName<ImpactThresholdDal>(x => x.MaxLossAmount)})
                VALUES ({EntityMapper.ParameterName(nameof(ImpactThresholdDal.ScaleId))}, {EntityMapper.ParameterName(nameof(ImpactThresholdDal.Weight))}, {EntityMapper.ParameterName(nameof(ImpactThresholdDal.MinLossAmount))}, {EntityMapper.ParameterName(nameof(ImpactThresholdDal.MaxLossAmount))})
                """;
            await connection.ExecuteAsync(thresholdQuery, thresholds, transaction);
            var operationId = Guid.NewGuid();
            foreach (var threshold in thresholds)
            {
                var before = previous?.Thresholds[threshold.Weight];
                await BusinessHistory.RecordAsync(transaction, operationId, actor.UserId, AssessmentMethod.ImpactId(threshold.Weight),
                    before is null ? null : new { before.MinLossAmount, before.MaxLossAmount },
                    new { threshold.MinLossAmount, threshold.MaxLossAmount }, EntityMapper.TbName<ImpactThresholdDal>(false));
            }
            var settings = await ReadAcceptanceSettingsAsync(transaction);
            await RecalculateAsync(transaction, actor.UserId, operationId, scale, settings);
            return scale;
        });
    }

    /// <inheritdoc/>
    public async Task<RiskAcceptanceSettingsDal> GetAcceptanceSettingsAsync(RiskActor actor)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, IdentityRights.AdminCode, transaction);
        var settings = await ReadAcceptanceSettingsAsync(transaction);
        transaction.Commit();
        return settings;
    }

    /// <inheritdoc/>
    public Task<RiskAcceptanceSettingsDal> SaveAcceptanceSettingsAsync(RiskActor actor, AcceptanceSettingsRequest request)
    {
        return WriteAsync(actor, IdentityRights.AdminCode, async transaction =>
        {
            var previous = await ReadAcceptanceSettingsAsync(transaction);
            RequireVersion(previous.Version, request.Version);
            var owner = request.OwnerMaxScore;
            var director = request.DirectorMaxScore;
            var ceo = request.CeoMaxScore;
            if (owner < 0 || owner >= director || director >= ceo || ceo >= AssessmentMethod.MaxScore)
            {
                throw new IdentityCommandException(400, RiskRules.Invalid);
            }
            var settings = previous;
            var operationId = Guid.NewGuid();
            if (owner != previous.OwnerMaxScore || director != previous.DirectorMaxScore || ceo != previous.CeoMaxScore)
            {
                var query = $"""
                    UPDATE {EntityMapper.TbName<RiskAcceptanceSettingsDal>()} SET {EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.OwnerMaxScore)} = {EntityMapper.ParameterName(nameof(owner))}, {EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.DirectorMaxScore)} = {EntityMapper.ParameterName(nameof(director))}, {EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.CeoMaxScore)} = {EntityMapper.ParameterName(nameof(ceo))},
                        {EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.UpdatedById)} = {EntityMapper.ParameterName(nameof(actor.UserId))}, {EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.UpdatedAt)} = clock_timestamp(), {EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.Version)} = gen_random_uuid()
                    WHERE {EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(previous.Id))} AND {EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(previous.Version))} RETURNING *
                    """;
                settings = await connection.QuerySingleOrDefaultAsync<RiskAcceptanceSettingsDal>(query,
                    new { previous.Id, previous.Version, owner, director, ceo, actor.UserId }, transaction)
                    ?? throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
                await BusinessHistory.RecordAsync(transaction, operationId, actor.UserId, settings.Id, previous, settings);
            }
            if (request.ApplyToExisting)
            {
                await RecalculateAsync(transaction, actor.UserId, operationId, null, settings);
            }
            return settings;
        });
    }

    /// <summary>Читает действующие пороги встроенного метода</summary>
    /// <param name="transaction">Транзакция операции</param>
    /// <returns>Текущие пороги</returns>
    private Task<RiskAcceptanceSettingsDal> ReadAcceptanceSettingsAsync(IDbTransaction transaction)
    {
        var id = AssessmentMethod.AcceptanceSettingsId;
        var query = $"SELECT * FROM {EntityMapper.TbName<RiskAcceptanceSettingsDal>()} WHERE {EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))}";
        return connection.QuerySingleAsync<RiskAcceptanceSettingsDal>(query, new { id }, transaction);
    }

    /// <summary>Возвращает опубликованную шкалу по идентификатору либо последнюю</summary>
    /// <param name="transaction">Транзакция операции</param>
    /// <param name="id">Запрошенная историческая версия</param>
    /// <returns>Шкала при наличии</returns>
    private async Task<ImpactScale?> ReadScaleAsync(IDbTransaction transaction, Guid? id = null)
    {
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<ImpactScaleDal>()} WHERE CAST({EntityMapper.ParameterName(nameof(id))} AS uuid) IS NULL OR {EntityMapper.ColName<ImpactScaleDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))}
            ORDER BY {EntityMapper.ColName<ImpactScaleDal>(x => x.Sequence)} DESC LIMIT 1
            """;
        var version = await connection.QuerySingleOrDefaultAsync<ImpactScaleDal>(query, new { id }, transaction);
        if (version is null)
        {
            return null;
        }

        var thresholdQuery = $"SELECT * FROM {EntityMapper.TbName<ImpactThresholdDal>()} WHERE {EntityMapper.ColName<ImpactThresholdDal>(x => x.ScaleId)} = {EntityMapper.ParameterName(nameof(version.Id))} ORDER BY {EntityMapper.ColName<ImpactThresholdDal>(x => x.Weight)}";
        var thresholds = (await connection.QueryAsync<ImpactThresholdDal>(thresholdQuery, new { version.Id }, transaction)).ToArray();
        return new ImpactScale(version, thresholds);
    }

    /// <summary>Требует опубликованную денежную шкалу для расчёта</summary>
    /// <param name="transaction">Транзакция операции</param>
    /// <returns>Действующая шкала</returns>
    private async Task<ImpactScale> RequireScaleAsync(IDbTransaction transaction)
    {
        return await ReadScaleAsync(transaction) ?? throw new IdentityCommandException(409, RiskRules.ScaleRequired);
    }
}
