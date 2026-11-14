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

/// <summary>Пользовательская оценка и хранение версий</summary>
internal sealed partial class RiskRepository
{
    /// <inheritdoc/>
    public async Task<AssessmentPreview> PreviewAssessmentAsync(RiskActor actor, Guid id, AssessmentRequest request)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
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
            var assessment = await InsertAssessmentAsync(transaction, preview.Assessment, actor.UserId);
            if (risk.Status == RiskRules.AwaitingReview)
            {
                await CompleteReviewAsync(id, actor.UserId, transaction, assessment.Id, null);
            }
            var updated = await SaveRiskStateAsync(transaction, risk, RiskRules.Active, preview.AcceptanceLevel);
            await BusinessHistory.RecordAsync(transaction, Guid.NewGuid(), actor.UserId, risk.Id, risk, updated,
                relatedPath: BusinessHistory.AssessmentsPath, relatedAfter: assessment);
            return preview with { RiskVersion = updated.Version, Assessment = assessment };
        });
    }

    /// <inheritdoc/>
    public async Task<RiskAssessmentDal[]> ListAssessmentsAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        await ReadAsync<RiskDal>(id, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<RiskAssessmentDal>()}
            WHERE {EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(id))}
            ORDER BY {EntityMapper.ColName<RiskAssessmentDal>(x => x.CreatedAt)} DESC,
                {EntityMapper.ColName<RiskAssessmentDal>(x => x.Id)} DESC
            OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var rows = await connection.QueryAsync<RiskAssessmentDal>(query, new { id, offset, limit }, transaction);
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
        await RequirePrimaryOwnerAsync(actor, id, transaction);
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
            if (await connection.ExecuteScalarAsync<bool>(existingQuery, new { id }, transaction))
            {
                throw new IdentityCommandException(409, RiskRules.InvalidTransition);
            }
        }
        else if (risk.Status != RiskRules.AwaitingReview || await ReadCurrentAssessmentAsync(id, transaction) is not null
            || await ReadWaitingReviewAsync(id, transaction) is null)
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
        var settings = await ReadAcceptanceSettingsAsync(transaction);
        return new AssessmentPreview(risk.Version, scale.Version.Id, assetVersion, assessment, AssessmentCalculation.Route(assessment.RiskScore, settings));
    }

    /// <summary>Читает единственную текущую оценку риска</summary>
    /// <param name="id">Риск</param>
    /// <param name="transaction">Транзакция операции</param>
    /// <returns>Текущая запись при наличии</returns>
    private Task<RiskAssessmentDal?> ReadCurrentAssessmentAsync(Guid id, IDbTransaction transaction)
    {
        var query = $"""SELECT * FROM {EntityMapper.TbName<RiskAssessmentDal>()} WHERE {EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(id))} AND {EntityMapper.ColName<RiskAssessmentDal>(x => x.IsActive)}""";
        return connection.QuerySingleOrDefaultAsync<RiskAssessmentDal>(query, new { id }, transaction);
    }

    /// <summary>Сохраняет неизменяемые входы и результаты новой оценки</summary>
    /// <param name="transaction">Транзакция изменения риска</param>
    /// <param name="assessment">Рассчитанное состояние</param>
    /// <param name="author">Автор версии</param>
    /// <returns>Созданная версия</returns>
    private Task<RiskAssessmentDal> InsertAssessmentAsync(IDbTransaction transaction, RiskAssessmentDal assessment, Guid author)
    {
        var row = assessment with { Id = Guid.NewGuid(), CreatedById = author };
        var query = $"""
            INSERT INTO {EntityMapper.TbName<RiskAssessmentDal>()}
                ({EntityMapper.ColName<RiskAssessmentDal>(x => x.Id)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskId)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.MethodId)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.ScaleVersionId)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.CreatedById)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.LikelihoodLevelId)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.ImpactLevelId)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskScore)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.LossAmount)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.DowntimeWeeks)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.DowntimeDays)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.DowntimeHours)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.DowntimeTotalHours)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.HourlyDowntimeCostSnapshot)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.ResponseMode)})
            VALUES ({EntityMapper.ParameterName(nameof(row.Id))}, {EntityMapper.ParameterName(nameof(row.RiskId))}, {EntityMapper.ParameterName(nameof(row.MethodId))}, {EntityMapper.ParameterName(nameof(row.ScaleVersionId))}, {EntityMapper.ParameterName(nameof(row.CreatedById))}, {EntityMapper.ParameterName(nameof(row.LikelihoodLevelId))}, {EntityMapper.ParameterName(nameof(row.ImpactLevelId))}, {EntityMapper.ParameterName(nameof(row.RiskScore))}, {EntityMapper.ParameterName(nameof(row.LossAmount))}, {EntityMapper.ParameterName(nameof(row.DowntimeWeeks))}, {EntityMapper.ParameterName(nameof(row.DowntimeDays))}, {EntityMapper.ParameterName(nameof(row.DowntimeHours))}, {EntityMapper.ParameterName(nameof(row.DowntimeTotalHours))}, {EntityMapper.ParameterName(nameof(row.HourlyDowntimeCostSnapshot))}, {EntityMapper.ParameterName(nameof(row.ResponseMode))}) RETURNING *
            """;
        return connection.QuerySingleAsync<RiskAssessmentDal>(query, row, transaction);
    }

    /// <summary>Меняет служебные поля версии оценки</summary>
    /// <param name="transaction">Транзакция команды</param>
    /// <param name="assessment">Ожидаемая версия оценки</param>
    /// <param name="active">Актуальность версии</param>
    /// <param name="mode">Режим текущего цикла</param>
    /// <returns>Обновлённые метаданные оценки</returns>
    private async Task<RiskAssessmentDal> SetAssessmentStateAsync(IDbTransaction transaction, RiskAssessmentDal assessment,
        bool active, string? mode)
    {
        var query = $"""
            UPDATE {EntityMapper.TbName<RiskAssessmentDal>()} SET {EntityMapper.ColName<RiskAssessmentDal>(x => x.IsActive)} = {EntityMapper.ParameterName(nameof(active))}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.ResponseMode)} = {EntityMapper.ParameterName(nameof(mode))}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.Version)} = gen_random_uuid()
            WHERE {EntityMapper.ColName<RiskAssessmentDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(assessment.Id))} AND {EntityMapper.ColName<RiskAssessmentDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(assessment.Version))} AND {EntityMapper.ColName<RiskAssessmentDal>(x => x.IsActive)} RETURNING *
            """;
        return await connection.QuerySingleOrDefaultAsync<RiskAssessmentDal>(query,
            new { assessment.Id, assessment.Version, active, mode }, transaction)
            ?? throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
    }

    /// <summary>Обновляет жизненный цикл, маршрут и конкурентную версию риска</summary>
    /// <param name="transaction">Транзакция изменения</param>
    /// <param name="risk">Прежняя карточка</param>
    /// <param name="status">Новый статус</param>
    /// <param name="level">Актуальный маршрут принятия</param>
    /// <param name="clearCloseReason">Признак очистки причины прежнего закрытия</param>
    /// <returns>Карточка после записи</returns>
    private async Task<RiskDal> SaveRiskStateAsync(IDbTransaction transaction, RiskDal risk, string status, string? level, bool clearCloseReason = false)
    {
        var query = $"""
            UPDATE {EntityMapper.TbName<RiskDal>()} SET {EntityMapper.ColName<RiskDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(status))}, {EntityMapper.ColName<RiskDal>(x => x.AcceptanceLevel)} = {EntityMapper.ParameterName(nameof(level))},
                {EntityMapper.ColName<RiskDal>(x => x.CloseReason)} = CASE WHEN {EntityMapper.ParameterName(nameof(clearCloseReason))} THEN NULL ELSE {EntityMapper.ColName<RiskDal>(x => x.CloseReason)} END,
                {EntityMapper.ColName<RiskDal>(x => x.Version)} = gen_random_uuid(), {EntityMapper.ColName<RiskDal>(x => x.UpdatedAt)} = clock_timestamp()
            WHERE {EntityMapper.ColName<RiskDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(risk.Id))} AND {EntityMapper.ColName<RiskDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(risk.Version))} RETURNING *
            """;
        return await connection.QuerySingleOrDefaultAsync<RiskDal>(query,
            new { risk.Id, risk.Version, status, level, clearCloseReason }, transaction)
            ?? throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
    }
}
