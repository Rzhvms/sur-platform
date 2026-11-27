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
using Infrastructure.Repositories.Sessions.Interfaces;
using Infrastructure.Repositories.Shared;
using System.Data;

namespace Infrastructure.Repositories.Assessments;

/// <summary>Оценки рисков и настройки метода</summary>
internal sealed class RiskAssessmentStateRepository : ClientRepository, IRiskAssessmentStateRepository
{
    /// <summary>Инициализирует операции выбранной клиентской базы</summary>
    /// <param name="connection">Подключение выбранной клиентской базы</param>
    /// <param name="sessions">Проверка сессии и эффективных прав в транзакции</param>
    /// <param name="operations">Метрики операций базы</param>
    public RiskAssessmentStateRepository(
        IDbConnection connection,
        ITransactionalSessionRepository sessions,
        OperationsMetrics operations)
        : base(connection, sessions, operations)
    {
    }

    /// <inheritdoc/>
    public Task<RiskAssessmentDal?> ReadCurrentAssessmentAsync(Guid id, IDbTransaction transaction)
    {
        var query = $"""SELECT * FROM {EntityMapper.TbName<RiskAssessmentDal>()} WHERE {EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(id))} AND {EntityMapper.ColName<RiskAssessmentDal>(x => x.IsActive)}""";
        return _connection.QuerySingleOrDefaultAsync<RiskAssessmentDal>(query, new { id }, transaction);
    }

    /// <inheritdoc/>
    public Task<RiskAssessmentDal> InsertAssessmentAsync(IDbTransaction transaction, RiskAssessmentDal assessment, Guid author)
    {
        var row = assessment with { Id = Guid.NewGuid(), CreatedById = author };
        var query = $"""
            INSERT INTO {EntityMapper.TbName<RiskAssessmentDal>()}
                ({EntityMapper.ColName<RiskAssessmentDal>(x => x.Id)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskId)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.MethodId)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.ScaleVersionId)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.CreatedById)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.LikelihoodLevelId)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.ImpactLevelId)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskScore)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.LossAmount)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.DowntimeWeeks)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.DowntimeDays)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.DowntimeHours)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.DowntimeTotalHours)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.HourlyDowntimeCostSnapshot)}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.ResponseMode)})
            VALUES ({EntityMapper.ParameterName(nameof(row.Id))}, {EntityMapper.ParameterName(nameof(row.RiskId))}, {EntityMapper.ParameterName(nameof(row.MethodId))}, {EntityMapper.ParameterName(nameof(row.ScaleVersionId))}, {EntityMapper.ParameterName(nameof(row.CreatedById))}, {EntityMapper.ParameterName(nameof(row.LikelihoodLevelId))}, {EntityMapper.ParameterName(nameof(row.ImpactLevelId))}, {EntityMapper.ParameterName(nameof(row.RiskScore))}, {EntityMapper.ParameterName(nameof(row.LossAmount))}, {EntityMapper.ParameterName(nameof(row.DowntimeWeeks))}, {EntityMapper.ParameterName(nameof(row.DowntimeDays))}, {EntityMapper.ParameterName(nameof(row.DowntimeHours))}, {EntityMapper.ParameterName(nameof(row.DowntimeTotalHours))}, {EntityMapper.ParameterName(nameof(row.HourlyDowntimeCostSnapshot))}, {EntityMapper.ParameterName(nameof(row.ResponseMode))}) RETURNING *
            """;
        return _connection.QuerySingleAsync<RiskAssessmentDal>(query, row, transaction);
    }

    /// <inheritdoc/>
    public async Task<RiskAssessmentDal> SetAssessmentStateAsync(IDbTransaction transaction, RiskAssessmentDal assessment,
        bool active, string? mode)
    {
        var query = $"""
            UPDATE {EntityMapper.TbName<RiskAssessmentDal>()} SET {EntityMapper.ColName<RiskAssessmentDal>(x => x.IsActive)} = {EntityMapper.ParameterName(nameof(active))}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.ResponseMode)} = {EntityMapper.ParameterName(nameof(mode))}, {EntityMapper.ColName<RiskAssessmentDal>(x => x.Version)} = gen_random_uuid()
            WHERE {EntityMapper.ColName<RiskAssessmentDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(assessment.Id))} AND {EntityMapper.ColName<RiskAssessmentDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(assessment.Version))} AND {EntityMapper.ColName<RiskAssessmentDal>(x => x.IsActive)} RETURNING *
            """;
        return await _connection.QuerySingleOrDefaultAsync<RiskAssessmentDal>(query,
            new { assessment.Id, assessment.Version, active, mode }, transaction)
            ?? throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
    }

    /// <inheritdoc/>
    public async Task<RiskDal> SaveRiskStateAsync(IDbTransaction transaction, RiskDal risk, string status, string? level, bool clearCloseReason = false)
    {
        var query = $"""
            UPDATE {EntityMapper.TbName<RiskDal>()} SET {EntityMapper.ColName<RiskDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(status))}, {EntityMapper.ColName<RiskDal>(x => x.AcceptanceLevel)} = {EntityMapper.ParameterName(nameof(level))},
                {EntityMapper.ColName<RiskDal>(x => x.CloseReason)} = CASE WHEN {EntityMapper.ParameterName(nameof(clearCloseReason))} THEN NULL ELSE {EntityMapper.ColName<RiskDal>(x => x.CloseReason)} END,
                {EntityMapper.ColName<RiskDal>(x => x.Version)} = gen_random_uuid(), {EntityMapper.ColName<RiskDal>(x => x.UpdatedAt)} = clock_timestamp()
            WHERE {EntityMapper.ColName<RiskDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(risk.Id))} AND {EntityMapper.ColName<RiskDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(risk.Version))} RETURNING *
            """;
        return await _connection.QuerySingleOrDefaultAsync<RiskDal>(query,
            new { risk.Id, risk.Version, status, level, clearCloseReason }, transaction)
            ?? throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
    }

    /// <inheritdoc/>
    public Task<RiskAcceptanceSettingsDal> ReadAcceptanceSettingsAsync(IDbTransaction transaction)
    {
        var id = AssessmentMethod.AcceptanceSettingsId;
        var query = $"SELECT * FROM {EntityMapper.TbName<RiskAcceptanceSettingsDal>()} WHERE {EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))}";
        return _connection.QuerySingleAsync<RiskAcceptanceSettingsDal>(query, new { id }, transaction);
    }

    /// <inheritdoc/>
    public async Task<ImpactScale?> ReadScaleAsync(IDbTransaction transaction, Guid? id = null)
    {
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<ImpactScaleDal>()} WHERE CAST({EntityMapper.ParameterName(nameof(id))} AS uuid) IS NULL OR {EntityMapper.ColName<ImpactScaleDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))}
            ORDER BY {EntityMapper.ColName<ImpactScaleDal>(x => x.Sequence)} DESC LIMIT 1
            """;
        var version = await _connection.QuerySingleOrDefaultAsync<ImpactScaleDal>(query, new { id }, transaction);
        if (version is null)
        {
            return null;
        }

        var thresholdQuery = $"SELECT * FROM {EntityMapper.TbName<ImpactThresholdDal>()} WHERE {EntityMapper.ColName<ImpactThresholdDal>(x => x.ScaleId)} = {EntityMapper.ParameterName(nameof(version.Id))} ORDER BY {EntityMapper.ColName<ImpactThresholdDal>(x => x.Weight)}";
        var thresholds = (await _connection.QueryAsync<ImpactThresholdDal>(thresholdQuery, new { version.Id }, transaction)).ToArray();
        return new ImpactScale(version, thresholds);
    }
}
