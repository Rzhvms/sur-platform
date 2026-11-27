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

/// <summary>Пересчитывает применимые оценки и маршруты в исходной транзакции</summary>
internal sealed partial class RiskRepository
{
    /// <inheritdoc/>
    public async Task RecalculateAssetAsync(IDbTransaction transaction, Guid actor, Guid operationId, AssetDal asset)
    {
        var scale = await ReadScaleAsync(transaction);
        var settings = await ReadAcceptanceSettingsAsync(transaction);
        await RecalculateAsync(transaction, actor, operationId, scale, settings, asset);
    }

    /// <summary>Фиксирует весь пакет технического пересчёта или изменения маршрутов</summary>
    /// <param name="transaction">Транзакция настройки или объекта</param>
    /// <param name="actor">Инициатор изменения</param>
    /// <param name="operationId">Общий идентификатор журнала</param>
    /// <param name="scale">Новая шкала для технических версий</param>
    /// <param name="settings">Пороги принятия</param>
    /// <param name="asset">Объект с новой стоимостью</param>
    private async Task RecalculateAsync(IDbTransaction transaction, Guid actor, Guid operationId,
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
        var rows = await connection.QueryAsync<RiskDal, RiskAssessmentDal, (RiskDal Risk, RiskAssessmentDal Assessment)>(
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
                var historical = await SetAssessmentStateAsync(transaction, assessment, false, assessment.ResponseMode);
                var created = await InsertAssessmentAsync(transaction, next with { ResponseMode = assessment.ResponseMode },
                    IdentityAccounts.SystemUserId);
                changes.Add((BusinessHistory.AssessmentsPath, assessment, historical));
                changes.Add((BusinessHistory.AssessmentsPath, null, created));
                score = created.RiskScore;
            }
            var route = AssessmentCalculation.Route(score, settings);
            if (changes.Count > 0 || route != risk.AcceptanceLevel)
            {
                var updated = await SaveRiskStateAsync(transaction, risk, risk.Status, route);
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
}
