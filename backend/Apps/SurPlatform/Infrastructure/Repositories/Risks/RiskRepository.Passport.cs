using Infrastructure.Repositories.Risks.Helpers;
using System.Data;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.UseCases.Risks.Dto;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;

namespace Infrastructure.Repositories.Risks;

/// <summary>Создание и редактирование паспорта черновика</summary>
internal sealed partial class RiskRepository
{
    /// <inheritdoc/>
    public Task<RiskDal> CreateAsync(RiskActor actor, RiskPassportRequest request)
    {
        return WriteAsync(actor, RiskRules.Create, transaction => CreateRiskAsync(actor, request, transaction));
    }

    /// <summary>Регистрирует риск и историю в транзакции вызывающей команды</summary>
    /// <param name="actor">Инициатор регистрации</param>
    /// <param name="request">Паспорт нового риска</param>
    /// <param name="transaction">Транзакция со всеми связанными изменениями</param>
    /// <returns>Созданный черновик</returns>
    private async Task<RiskDal> CreateRiskAsync(RiskActor actor, RiskPassportRequest request, IDbTransaction transaction)
    {
        if (request.ProcessId is not Guid processId || request.Version is not null)
        {
            throw new IdentityCommandException(400, RiskRules.Invalid);
        }

        var process = await ReadAsync<ProcessDal>(processId, EntityMapper.ColName<ProcessDal>(x => x.Id), transaction);
        var directorate = await ReadAsync<DirectorateDal>(process.DirectorateId, EntityMapper.ColName<DirectorateDal>(x => x.Id), transaction);
        if (!process.IsActive || !directorate.IsActive)
        {
            throw new IdentityCommandException(409, RiskRules.InactiveReference);
        }

        if (!await HasRightAsync(actor, IdentityRights.AdminCode, transaction))
        {
            var userId = actor.UserId;
            var directorateId = process.DirectorateId;
            var contextQuery = $"""
                SELECT EXISTS (SELECT FROM {EntityMapper.TbName<OrgPersonDal>()}
                    WHERE {EntityMapper.ColName<OrgPersonDal>(x => x.UserId)} = {EntityMapper.ParameterName(nameof(userId))} AND {EntityMapper.ColName<OrgPersonDal>(x => x.IsActive)} AND {EntityMapper.ColName<OrgPersonDal>(x => x.DirectorateId)} = {EntityMapper.ParameterName(nameof(directorateId))})
                """;
            if (!await connection.ExecuteScalarAsync<bool>(contextQuery, new { userId, directorateId }, transaction))
            {
                throw new IdentityCommandException(403, IdentityErrorCodes.AccessDenied);
            }
        }

        await ValidatePassportAsync(request, null, transaction);
        var name = RequiredText(request.Name);
        var description = RequiredText(request.Description);
        var draft = RiskRules.Draft;
        var authorId = actor.UserId;
        var query = $"""
            INSERT INTO {EntityMapper.TbName<RiskDal>()} ({EntityMapper.ColName<RiskDal>(x => x.Id)}, {EntityMapper.ColName<RiskDal>(x => x.Name)}, {EntityMapper.ColName<RiskDal>(x => x.Description)}, {EntityMapper.ColName<RiskDal>(x => x.HazardId)}, {EntityMapper.ColName<RiskDal>(x => x.CategoryId)},
                {EntityMapper.ColName<RiskDal>(x => x.AssetId)}, {EntityMapper.ColName<RiskDal>(x => x.AuthorId)}, {EntityMapper.ColName<RiskDal>(x => x.Status)})
            VALUES (gen_random_uuid(), {EntityMapper.ParameterName(nameof(name))}, {EntityMapper.ParameterName(nameof(description))}, {EntityMapper.ParameterName(nameof(request.HazardId))},
                {EntityMapper.ParameterName(nameof(request.CategoryId))}, {EntityMapper.ParameterName(nameof(request.AssetId))}, {EntityMapper.ParameterName(nameof(authorId))}, {EntityMapper.ParameterName(nameof(draft))})
            RETURNING *
            """;
        var risk = await connection.QuerySingleAsync<RiskDal>(query,
            new { name, description, request.HazardId, request.CategoryId, request.AssetId, authorId, draft }, transaction);
        var riskId = risk.Id;
        var ownerId = process.OwnerId;
        var linkQuery = $"""
            INSERT INTO {EntityMapper.TbName<RiskProcessDal>()} ({EntityMapper.ColName<RiskProcessDal>(x => x.Id)}, {EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)}, {EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)},
                {EntityMapper.ColName<RiskProcessDal>(x => x.OwnerId)}, {EntityMapper.ColName<RiskProcessDal>(x => x.IsPrimary)})
            VALUES (gen_random_uuid(), {EntityMapper.ParameterName(nameof(riskId))}, {EntityMapper.ParameterName(nameof(processId))}, {EntityMapper.ParameterName(nameof(ownerId))}, true)
            RETURNING *
            """;
        var link = await connection.QuerySingleAsync<RiskProcessDal>(linkQuery, new { riskId, processId, ownerId }, transaction);
        var operationId = Guid.NewGuid();
        await BusinessHistory.RecordAsync(transaction, operationId, actor.UserId, risk.Id, null, risk,
            relatedPath: BusinessHistory.ProcessesPath, relatedAfter: link);
        return risk;
    }

    /// <inheritdoc/>
    public Task<RiskDal> UpdateAsync(RiskActor actor, Guid id, RiskPassportRequest request)
    {
        return WriteAsync(actor, RiskRules.Edit, async transaction =>
        {
            var previous = await ReadAsync<RiskDal>(id, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
            await RequirePrimaryOwnerAsync(actor, id, transaction);
            RequireVersion(previous.Version, request.Version);
            if (request.ProcessId is not null)
            {
                throw new IdentityCommandException(400, RiskRules.Invalid);
            }

            var assessmentQuery = $"SELECT EXISTS (SELECT FROM {EntityMapper.TbName<RiskAssessmentDal>()} WHERE {EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(id))})";
            if (await connection.ExecuteScalarAsync<bool>(assessmentQuery, new { id }, transaction))
            {
                throw new IdentityCommandException(409, RiskRules.PassportLocked);
            }

            await ValidatePassportAsync(request, previous, transaction);
            var name = RequiredText(request.Name);
            var description = RequiredText(request.Description);
            if (name == previous.Name && description == previous.Description && request.HazardId == previous.HazardId
                && request.CategoryId == previous.CategoryId && request.AssetId == previous.AssetId)
            {
                return previous;
            }

            var version = previous.Version;
            var query = $"""
                UPDATE {EntityMapper.TbName<RiskDal>()} SET {EntityMapper.ColName<RiskDal>(x => x.Name)} = {EntityMapper.ParameterName(nameof(name))}, {EntityMapper.ColName<RiskDal>(x => x.Description)} = {EntityMapper.ParameterName(nameof(description))},
                    {EntityMapper.ColName<RiskDal>(x => x.HazardId)} = {EntityMapper.ParameterName(nameof(request.HazardId))},
                    {EntityMapper.ColName<RiskDal>(x => x.CategoryId)} = {EntityMapper.ParameterName(nameof(request.CategoryId))},
                    {EntityMapper.ColName<RiskDal>(x => x.AssetId)} = {EntityMapper.ParameterName(nameof(request.AssetId))},
                    {EntityMapper.ColName<RiskDal>(x => x.Version)} = gen_random_uuid(), {EntityMapper.ColName<RiskDal>(x => x.UpdatedAt)} = clock_timestamp()
                WHERE {EntityMapper.ColName<RiskDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))} AND {EntityMapper.ColName<RiskDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(version))} RETURNING *
                """;
            var updated = await connection.QuerySingleOrDefaultAsync<RiskDal>(query,
                new { id, version, name, description, request.HazardId, request.CategoryId, request.AssetId }, transaction)
                ?? throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
            await BusinessHistory.RecordAsync(transaction, Guid.NewGuid(), actor.UserId, id, previous, updated);
            return updated;
        });
    }

    /// <summary>Проверяет доступность новых связей и сохраняет существующие исторические ссылки</summary>
    /// <param name="request">Запрошенный паспорт</param>
    /// <param name="previous">Предыдущий паспорт при редактировании</param>
    /// <param name="transaction">Транзакция команды</param>
    private async Task ValidatePassportAsync(RiskPassportRequest request, RiskDal? previous, IDbTransaction transaction)
    {
        if (request.HazardId != previous?.HazardId)
        {
            var hazard = await ReadAsync<HazardDal>(request.HazardId, EntityMapper.ColName<HazardDal>(x => x.Id), transaction);
            var group = await ReadAsync<HazardGroupDal>(hazard.GroupId, EntityMapper.ColName<HazardGroupDal>(x => x.Id), transaction);
            if (!hazard.IsActive || !group.IsActive)
            {
                throw new IdentityCommandException(409, RiskRules.InactiveReference);
            }
        }

        if (request.CategoryId != previous?.CategoryId &&
            !(await ReadAsync<RiskCategoryDal>(request.CategoryId, EntityMapper.ColName<RiskCategoryDal>(x => x.Id), transaction)).IsActive)
        {
            throw new IdentityCommandException(409, RiskRules.InactiveReference);
        }

        if (request.AssetId is Guid assetId && assetId != previous?.AssetId)
        {
            var asset = await ReadAsync<AssetDal>(assetId, EntityMapper.ColName<AssetDal>(x => x.Id), transaction);
            if (!asset.IsActive || asset.HourlyDowntimeCost < 0 || decimal.Round(asset.HourlyDowntimeCost, 2) != asset.HourlyDowntimeCost)
            {
                throw new IdentityCommandException(409, RiskRules.InactiveReference);
            }
        }
    }
}
