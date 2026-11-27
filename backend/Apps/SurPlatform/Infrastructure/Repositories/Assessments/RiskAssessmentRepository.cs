using Infrastructure.Repositories.Assessments.Helpers;
using Infrastructure.Repositories.History.Helpers;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.Ports.Repositories;
using Application.UseCases.Risks.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Observability;
using Infrastructure.Repositories.Assessments.Interfaces;
using Infrastructure.Repositories.Reviews.Interfaces;
using Infrastructure.Repositories.Risks.Interfaces;
using Infrastructure.Repositories.Sessions.Interfaces;
using Infrastructure.Repositories.Shared;
using System.Data;

namespace Infrastructure.Repositories.Assessments;

/// <summary>Оценки рисков и настройки метода</summary>
internal sealed class RiskAssessmentRepository : ClientRepository, IRiskAssessmentRepository
{
    /// <summary>Запуск и завершение пересмотра в общей транзакции</summary>
    private readonly IReviewStateRepository _reviewStateRepository;

    /// <summary>Текущая оценка и версии состояния риска</summary>
    private readonly IRiskAssessmentStateRepository _riskAssessmentStateRepository;

    /// <summary>Паспорт, связи процессов и технический пересчет риска</summary>
    private readonly IRiskStateRepository _riskStateRepository;

    /// <summary>Инициализирует операции выбранной клиентской базы</summary>
    /// <param name="connection">Подключение выбранной клиентской базы</param>
    /// <param name="sessions">Проверка сессии и эффективных прав в транзакции</param>
    /// <param name="operations">Метрики операций базы</param>
    /// <param name="reviewStateRepository">Запуск и завершение пересмотра в общей транзакции</param>
    /// <param name="riskAssessmentStateRepository">Текущая оценка и версии состояния риска</param>
    /// <param name="riskStateRepository">Паспорт, связи процессов и технический пересчет риска</param>
    public RiskAssessmentRepository(
        IDbConnection connection,
        ITransactionalSessionRepository sessions,
        OperationsMetrics operations,
        IReviewStateRepository reviewStateRepository,
        IRiskAssessmentStateRepository riskAssessmentStateRepository,
        IRiskStateRepository riskStateRepository)
        : base(connection, sessions, operations)
    {
        _reviewStateRepository = reviewStateRepository;
        _riskAssessmentStateRepository = riskAssessmentStateRepository;
        _riskStateRepository = riskStateRepository;
    }

    /// <inheritdoc/>
    public async Task<AssessmentPreview> PreviewAssessmentAsync(RiskActor actor, Guid id, AssessmentRequest request)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.Assess, transaction);
        var risk = await RequireAssessableRiskAsync(actor, id, transaction);
        var preview = await CalculateAssessmentAsync(risk, request, transaction);
        transaction.Commit();
        return preview;
    }

    /// <inheritdoc/>
    public Task<AssessmentPreview> SaveAssessmentAsync(RiskActor actor, Guid id, SaveAssessmentRequest request)
    {
        return WriteAsync(actor, RiskRules.Assess, async transaction =>
        {
            var risk = await RequireAssessableRiskAsync(actor, id, transaction, request.Version);
            var inputs = request.Inputs ?? throw new IdentityCommandException(400, RiskRules.Invalid);
            var preview = await CalculateAssessmentAsync(risk, inputs, transaction, request.ScaleVersionId);
            if (preview.AssetVersion != request.AssetVersion)
            {
                throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
            }
            var assessment = await _riskAssessmentStateRepository.InsertAssessmentAsync(transaction, preview.Assessment, actor.UserId);
            if (risk.Status == RiskRules.AwaitingReview)
            {
                await _reviewStateRepository.CompleteReviewAsync(id, actor.UserId, transaction, assessment.Id, null);
            }
            var updated = await _riskAssessmentStateRepository.SaveRiskStateAsync(transaction, risk, RiskRules.Active, preview.AcceptanceLevel);
            await BusinessHistory.RecordAsync(transaction, Guid.NewGuid(), actor.UserId, risk.Id, risk, updated,
                relatedPath: BusinessHistory.AssessmentsPath, relatedAfter: assessment);
            return preview with { RiskVersion = updated.Version, Assessment = assessment };
        });
    }

    /// <inheritdoc/>
    public async Task<RiskAssessmentDal[]> ListAssessmentsAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        await ReadAsync<RiskDal>(id, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<RiskAssessmentDal>()}
            WHERE {EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(id))}
            ORDER BY {EntityMapper.ColName<RiskAssessmentDal>(x => x.CreatedAt)} DESC,
                {EntityMapper.ColName<RiskAssessmentDal>(x => x.Id)} DESC
            OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var rows = await _connection.QueryAsync<RiskAssessmentDal>(query, new { id, offset, limit }, transaction);
        transaction.Commit();
        return rows.ToArray();
    }

    /// <summary>Проверяет контекст и статус бизнес-оценки</summary>
    /// <param name="actor">Исполнитель</param>
    /// <param name="id">Риск</param>
    /// <param name="transaction">Снимок бизнес-операции</param>
    /// <param name="version">Ожидаемая версия для записи</param>
    /// <returns>Риск, допускающий новую оценку</returns>
    private async Task<RiskDal> RequireAssessableRiskAsync(RiskActor actor, Guid id, IDbTransaction transaction, Guid? version = null)
    {
        var risk = await ReadAsync<RiskDal>(id, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
        await _riskStateRepository.RequirePrimaryOwnerAsync(actor, id, transaction);
        if (version.HasValue)
        {
            RequireVersion(risk.Version, version);
        }
        if (risk.Status == RiskRules.Draft)
        {
            await RequireRightAsync(actor, RiskRules.Edit, transaction);
            var existingQuery = $"""
                SELECT EXISTS (SELECT FROM {EntityMapper.TbName<RiskAssessmentDal>()}
                    WHERE {EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(id))})
                """;
            if (await _connection.ExecuteScalarAsync<bool>(existingQuery, new { id }, transaction))
            {
                throw new IdentityCommandException(409, RiskRules.InvalidTransition);
            }
        }
        else if (risk.Status != RiskRules.AwaitingReview || await _riskAssessmentStateRepository.ReadCurrentAssessmentAsync(id, transaction) is not null
            || await _reviewStateRepository.ReadWaitingReviewAsync(id, transaction) is null)
        {
            throw new IdentityCommandException(409, RiskRules.InvalidTransition);
        }
        return risk;
    }

    /// <summary>Рассчитывает оценку в согласованном снимке</summary>
    /// <param name="risk">Риск и его объект</param>
    /// <param name="inputs">Ввод формы</param>
    /// <param name="transaction">Транзакция расчёта</param>
    /// <param name="scaleVersion">Ожидаемая версия шкалы</param>
    /// <returns>Оценка и маршрут</returns>
    private async Task<AssessmentPreview> CalculateAssessmentAsync(RiskDal risk, AssessmentRequest inputs,
        IDbTransaction transaction, Guid? scaleVersion = null)
    {
        var scale = await RequireScaleAsync(transaction);
        if (scaleVersion.HasValue)
        {
            RequireVersion(scale.Version.Id, scaleVersion);
        }
        decimal? rate = null;
        Guid? assetVersion = null;
        if (risk.AssetId is Guid assetId)
        {
            var asset = await ReadAsync<AssetDal>(assetId, EntityMapper.ColName<AssetDal>(x => x.Id), transaction);
            rate = asset.HourlyDowntimeCost;
            assetVersion = asset.Version;
        }
        var assessment = AssessmentCalculation.Calculate(risk.Id, inputs, rate, scale);
        var settings = await _riskAssessmentStateRepository.ReadAcceptanceSettingsAsync(transaction);
        return new AssessmentPreview(risk.Version, scale.Version.Id, assetVersion, assessment, AssessmentCalculation.Route(assessment.RiskScore, settings));
    }

    /// <inheritdoc/>
    public async Task<ImpactScale?> GetImpactScaleAsync(RiskActor actor, Guid? id, bool admin)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        if (admin)
        {
            await RequireRightAsync(actor, IdentityRights.AdminCode, transaction);
        }
        else if (!await HasRightAsync(actor, RiskRules.View, transaction) && !await HasRightAsync(actor, RiskRules.Assess, transaction))
        {
            throw new IdentityCommandException(403, IdentityErrorCodes.AccessDenied);
        }
        var scale = await _riskAssessmentStateRepository.ReadScaleAsync(transaction, id);
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
            var previous = await _riskAssessmentStateRepository.ReadScaleAsync(transaction);
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
            var scale = new ImpactScale(await _connection.QuerySingleAsync<ImpactScaleDal>(query, row, transaction), thresholds);
            var thresholdQuery = $"""
                INSERT INTO {EntityMapper.TbName<ImpactThresholdDal>()}
                    ({EntityMapper.ColName<ImpactThresholdDal>(x => x.ScaleId)}, {EntityMapper.ColName<ImpactThresholdDal>(x => x.Weight)}, {EntityMapper.ColName<ImpactThresholdDal>(x => x.MinLossAmount)}, {EntityMapper.ColName<ImpactThresholdDal>(x => x.MaxLossAmount)})
                VALUES ({EntityMapper.ParameterName(nameof(ImpactThresholdDal.ScaleId))}, {EntityMapper.ParameterName(nameof(ImpactThresholdDal.Weight))}, {EntityMapper.ParameterName(nameof(ImpactThresholdDal.MinLossAmount))}, {EntityMapper.ParameterName(nameof(ImpactThresholdDal.MaxLossAmount))})
                """;
            await _connection.ExecuteAsync(thresholdQuery, thresholds, transaction);
            var operationId = Guid.NewGuid();
            foreach (var threshold in thresholds)
            {
                var before = previous?.Thresholds[threshold.Weight];
                await BusinessHistory.RecordAsync(transaction, operationId, actor.UserId, AssessmentMethod.ImpactId(threshold.Weight),
                    before is null ? null : new { before.MinLossAmount, before.MaxLossAmount },
                    new { threshold.MinLossAmount, threshold.MaxLossAmount }, EntityMapper.TbName<ImpactThresholdDal>(false));
            }
            var settings = await _riskAssessmentStateRepository.ReadAcceptanceSettingsAsync(transaction);
            await _riskStateRepository.RecalculateAsync(transaction, actor.UserId, operationId, scale, settings);
            return scale;
        });
    }

    /// <inheritdoc/>
    public async Task<RiskAcceptanceSettingsDal> GetAcceptanceSettingsAsync(RiskActor actor)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, IdentityRights.AdminCode, transaction);
        var settings = await _riskAssessmentStateRepository.ReadAcceptanceSettingsAsync(transaction);
        transaction.Commit();
        return settings;
    }

    /// <inheritdoc/>
    public Task<RiskAcceptanceSettingsDal> SaveAcceptanceSettingsAsync(RiskActor actor, AcceptanceSettingsRequest request)
    {
        return WriteAsync(actor, IdentityRights.AdminCode, async transaction =>
        {
            var previous = await _riskAssessmentStateRepository.ReadAcceptanceSettingsAsync(transaction);
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
                settings = await _connection.QuerySingleOrDefaultAsync<RiskAcceptanceSettingsDal>(query,
                    new { previous.Id, previous.Version, owner, director, ceo, actor.UserId }, transaction)
                    ?? throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
                await BusinessHistory.RecordAsync(transaction, operationId, actor.UserId, settings.Id, previous, settings);
            }
            if (request.ApplyToExisting)
            {
                await _riskStateRepository.RecalculateAsync(transaction, actor.UserId, operationId, null, settings);
            }
            return settings;
        });
    }

    /// <summary>Требует опубликованную денежную шкалу для расчёта</summary>
    /// <param name="transaction">Транзакция операции</param>
    /// <returns>Действующая шкала</returns>
    private async Task<ImpactScale> RequireScaleAsync(IDbTransaction transaction)
    {
        return await _riskAssessmentStateRepository.ReadScaleAsync(transaction) ?? throw new IdentityCommandException(409, RiskRules.ScaleRequired);
    }
}
