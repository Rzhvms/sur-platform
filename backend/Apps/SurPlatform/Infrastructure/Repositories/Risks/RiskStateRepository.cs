using Infrastructure.Repositories.Assessments.Helpers;
using Infrastructure.Repositories.History.Helpers;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.Ports.Repositories;
using Application.UseCases.Risks.Dto.Request;
using Application.UseCases.Risks.Dto;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Clients;
using Infrastructure.Observability;
using Infrastructure.Repositories.Assessments.Interfaces;
using Infrastructure.Repositories.Identity;
using Infrastructure.Repositories.Risks.Interfaces;
using Infrastructure.Repositories.Sessions.Interfaces;
using Infrastructure.Repositories.Shared;
using Infrastructure.Services.Documents;
using Infrastructure.Services.Jira;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace Infrastructure.Repositories.Risks;

/// <summary>Реестр рисков и операции с паспортом</summary>
internal sealed class RiskStateRepository : ClientRepository, IRiskStateRepository
{
    /// <summary>Текущая оценка и версии состояния риска</summary>
    private readonly IRiskAssessmentStateRepository _riskAssessmentStateRepository;

    /// <summary>Инициализирует операции выбранной клиентской базы</summary>
    /// <param name="connection">Подключение выбранной клиентской базы</param>
    /// <param name="sessions">Проверка сессии и эффективных прав в транзакции</param>
    /// <param name="operations">Метрики операций базы</param>
    /// <param name="riskAssessmentStateRepository">Текущая оценка и версии состояния риска</param>
    public RiskStateRepository(
        IDbConnection connection,
        ITransactionalSessionRepository sessions,
        OperationsMetrics operations,
        IRiskAssessmentStateRepository riskAssessmentStateRepository)
        : base(connection, sessions, operations)
    {
        _riskAssessmentStateRepository = riskAssessmentStateRepository;
    }

    /// <inheritdoc/>
    public async Task<ProcessDal[]> ReadActiveProcessesAsync(Guid[] processIds, IDbTransaction transaction)
    {
        var readProcessesQuery = $"""
            SELECT process.* FROM {EntityMapper.TbName<ProcessDal>()} process
            JOIN {EntityMapper.TbName<DirectorateDal>()} directorate ON directorate.{EntityMapper.ColName<DirectorateDal>(x => x.Id)} = process.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)} AND directorate.{EntityMapper.ColName<DirectorateDal>(x => x.IsActive)}
            JOIN {EntityMapper.TbName<OrgPersonDal>()} person ON person.{EntityMapper.ColName<OrgPersonDal>(x => x.Id)} = process.{EntityMapper.ColName<ProcessDal>(x => x.OwnerId)} AND person.{EntityMapper.ColName<OrgPersonDal>(x => x.IsActive)}
            JOIN {EntityMapper.TbName<UserProfileDal>()} profile ON profile.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = person.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)} AND profile.{EntityMapper.ColName<UserProfileDal>(x => x.IsActive)}
            WHERE process.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = ANY({EntityMapper.ParameterName(nameof(processIds))}) AND process.{EntityMapper.ColName<ProcessDal>(x => x.IsActive)}
            """;
        var processes = (await _connection.QueryAsync<ProcessDal>(readProcessesQuery, new { processIds }, transaction)).ToArray();
        if (processes.Length != processIds.Length)
        {
            throw new IdentityCommandException(409, RiskRules.InactiveReference);
        }

        return processes;
    }

    /// <inheritdoc/>
    public async Task<RiskProcessDal[]> ReadActiveRiskLinksAsync(Guid id, IDbTransaction transaction)
    {
        var readLinksQuery = $"""
            SELECT * FROM {EntityMapper.TbName<RiskProcessDal>()} WHERE {EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(id))} AND {EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL
            ORDER BY {EntityMapper.ColName<RiskProcessDal>(x => x.IsPrimary)} DESC, {EntityMapper.ColName<RiskProcessDal>(x => x.Id)}
            """;
        return (await _connection.QueryAsync<RiskProcessDal>(readLinksQuery, new { id }, transaction)).ToArray();
    }

    /// <inheritdoc/>
    public async Task<RiskProcessDal> InsertProcessLinkAsync(IDbTransaction transaction, Guid id, ProcessDal process, bool isPrimary)
    {
        var row = new RiskProcessDal { Id = Guid.NewGuid(), RiskId = id, ProcessId = process.Id,
            OwnerId = process.OwnerId, IsPrimary = isPrimary };
        var insertQuery = $"""
            INSERT INTO {EntityMapper.TbName<RiskProcessDal>()}
                ({EntityMapper.ColName<RiskProcessDal>(x => x.Id)}, {EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)}, {EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)}, {EntityMapper.ColName<RiskProcessDal>(x => x.OwnerId)}, {EntityMapper.ColName<RiskProcessDal>(x => x.IsPrimary)})
            VALUES ({EntityMapper.ParameterName(nameof(row.Id))}, {EntityMapper.ParameterName(nameof(row.RiskId))}, {EntityMapper.ParameterName(nameof(row.ProcessId))}, {EntityMapper.ParameterName(nameof(row.OwnerId))}, {EntityMapper.ParameterName(nameof(row.IsPrimary))}) RETURNING *
            """;
        return await _connection.QuerySingleAsync<RiskProcessDal>(insertQuery, row, transaction);
    }

    /// <inheritdoc/>
    public async Task<RiskProcessDal> SaveProcessLinkAsync(IDbTransaction transaction, RiskProcessDal link, bool primary, bool keep)
    {
        var query = $"""
            UPDATE {EntityMapper.TbName<RiskProcessDal>()} SET {EntityMapper.ColName<RiskProcessDal>(x => x.IsPrimary)} = {EntityMapper.ParameterName(nameof(primary))}, {EntityMapper.ColName<RiskProcessDal>(x => x.Version)} = gen_random_uuid(),
                {EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} = CASE WHEN {EntityMapper.ParameterName(nameof(keep))} THEN NULL ELSE clock_timestamp() END
            WHERE {EntityMapper.ColName<RiskProcessDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(link.Id))} AND {EntityMapper.ColName<RiskProcessDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(link.Version))} AND {EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL RETURNING *
            """;
        return await _connection.QuerySingleOrDefaultAsync<RiskProcessDal>(query, new { link.Id, link.Version, primary, keep }, transaction)
            ?? throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
    }

    /// <inheritdoc/>
    public async Task<RiskDal> CreateRiskAsync(RiskActor actor, RiskPassportRequest request, IDbTransaction transaction)
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
            if (!await _connection.ExecuteScalarAsync<bool>(contextQuery, new { userId, directorateId }, transaction))
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
        var risk = await _connection.QuerySingleAsync<RiskDal>(query,
            new { name, description, request.HazardId, request.CategoryId, request.AssetId, authorId, draft }, transaction);
        var riskId = risk.Id;
        var ownerId = process.OwnerId;
        var linkQuery = $"""
            INSERT INTO {EntityMapper.TbName<RiskProcessDal>()} ({EntityMapper.ColName<RiskProcessDal>(x => x.Id)}, {EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)}, {EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)},
                {EntityMapper.ColName<RiskProcessDal>(x => x.OwnerId)}, {EntityMapper.ColName<RiskProcessDal>(x => x.IsPrimary)})
            VALUES (gen_random_uuid(), {EntityMapper.ParameterName(nameof(riskId))}, {EntityMapper.ParameterName(nameof(processId))}, {EntityMapper.ParameterName(nameof(ownerId))}, true)
            RETURNING *
            """;
        var link = await _connection.QuerySingleAsync<RiskProcessDal>(linkQuery, new { riskId, processId, ownerId }, transaction);
        var operationId = Guid.NewGuid();
        await BusinessHistory.RecordAsync(transaction, operationId, actor.UserId, risk.Id, null, risk,
            relatedPath: BusinessHistory.ProcessesPath, relatedAfter: link);
        return risk;
    }

    /// <inheritdoc/>
    public async Task ValidatePassportAsync(RiskPassportRequest request, RiskDal? previous, IDbTransaction transaction)
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

    /// <inheritdoc/>
    public async Task RecalculateAsync(IDbTransaction transaction, Guid actor, Guid operationId,
        ImpactScale? scale, RiskAcceptanceSettingsDal settings, AssetDal? asset = null)
    {
        var assetId = asset?.Id;
        var statuses = new[] { RiskRules.Active, RiskRules.InProgress };
        var query = $"""
            SELECT r.*, a.*
            FROM {EntityMapper.TbName<RiskDal>()} r JOIN {EntityMapper.TbName<RiskAssessmentDal>()} a
              ON a.{EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskId)} = r.{EntityMapper.ColName<RiskDal>(x => x.Id)} AND a.{EntityMapper.ColName<RiskAssessmentDal>(x => x.IsActive)}
            WHERE r.{EntityMapper.ColName<RiskDal>(x => x.Status)} = ANY({EntityMapper.ParameterName(nameof(statuses))})
              AND (CAST({EntityMapper.ParameterName(nameof(assetId))} AS uuid) IS NULL OR r.{EntityMapper.ColName<RiskDal>(x => x.AssetId)} = {EntityMapper.ParameterName(nameof(assetId))})
            ORDER BY r.{EntityMapper.ColName<RiskDal>(x => x.Id)}
            """;
        var rows = await _connection.QueryAsync<RiskDal, RiskAssessmentDal, (RiskDal Risk, RiskAssessmentDal Assessment)>(
            query, (risk, assessment) => (risk, assessment), new { assetId, statuses }, transaction,
            splitOn: EntityMapper.ColName<RiskAssessmentDal>(x => x.Id, false));
        foreach (var (risk, assessment) in rows)
        {
            var changes = new List<(string Path, object? Before, object After)>();
            var score = assessment.RiskScore;
            if (scale is not null || asset is not null)
            {
                if (scale is null || assessment.LossAmount is null ||
                    (risk.AssetId.HasValue && assessment.HourlyDowntimeCostSnapshot is null))
                {
                    throw new IdentityCommandException(409, RiskRules.AssessmentInputRequired);
                }
                var next = CalculateTechnicalVersion(risk, assessment, scale, asset);
                var historical = await _riskAssessmentStateRepository.SetAssessmentStateAsync(transaction, assessment, false, assessment.ResponseMode);
                var created = await _riskAssessmentStateRepository.InsertAssessmentAsync(transaction, next with { ResponseMode = assessment.ResponseMode },
                    IdentityAccounts.SystemUserId);
                changes.Add((BusinessHistory.AssessmentsPath, assessment, historical));
                changes.Add((BusinessHistory.AssessmentsPath, null, created));
                score = created.RiskScore;
            }
            var route = AssessmentCalculation.Route(score, settings);
            if (changes.Count > 0 || route != risk.AcceptanceLevel)
            {
                var updated = await _riskAssessmentStateRepository.SaveRiskStateAsync(transaction, risk, risk.Status, route);
                await BusinessHistory.RecordAsync(transaction, operationId, actor, risk.Id, risk, updated,
                    system: true, relatedChanges: changes);
            }
        }
    }

    /// <summary>Использует сохранённые входы и при необходимости новую ставку объекта</summary>
    /// <param name="risk">Пересчитываемый риск</param>
    /// <param name="assessment">Результат текущего цикла</param>
    /// <param name="scale">Применяемая шкала</param>
    /// <param name="asset">Объект с изменённой ставкой</param>
    /// <returns>Результат технического расчёта</returns>
    private static RiskAssessmentDal CalculateTechnicalVersion(RiskDal risk, RiskAssessmentDal assessment, ImpactScale scale, AssetDal? asset)
    {
        if (asset is null)
        {
            var loss = assessment.LossAmount ?? throw new IdentityCommandException(409, RiskRules.AssessmentInputRequired);
            var weight = AssessmentMethod.GetLikelihoodWeight(assessment.LikelihoodLevelId)
                ?? throw new IdentityCommandException(409, RiskRules.AssessmentInputRequired);
            var impact = AssessmentCalculation.FindImpact(loss, scale);
            return assessment with { ScaleVersionId = scale.Version.Id, ImpactLevelId = AssessmentMethod.ImpactId(impact), RiskScore = weight * impact };
        }
        var inputs = new AssessmentRequest
        {
            LikelihoodLevelId = assessment.LikelihoodLevelId, DowntimeWeeks = assessment.DowntimeWeeks,
            DowntimeDays = assessment.DowntimeDays, DowntimeHours = assessment.DowntimeHours
        };
        return AssessmentCalculation.Calculate(risk.Id, inputs, asset.HourlyDowntimeCost, scale);
    }

    /// <inheritdoc/>
    public async Task RequirePrimaryOwnerAsync(RiskActor actor, Guid riskId, IDbTransaction transaction)
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
        if (!await _connection.ExecuteScalarAsync<bool>(query, new { riskId, userId }, transaction))
        {
            throw new IdentityCommandException(403, IdentityErrorCodes.AccessDenied);
        }
    }
}
