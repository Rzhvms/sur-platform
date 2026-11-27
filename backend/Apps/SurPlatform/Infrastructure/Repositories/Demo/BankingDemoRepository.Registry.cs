using Infrastructure.Repositories.Assessments.Helpers;
using Infrastructure.Repositories.History.Helpers;
using System.Data;
using Application.Constants;
using Application.Models;
using Application.UseCases.Risks.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Repositories.Demo.Interfaces;
using Infrastructure.Repositories.Demo.Models;

namespace Infrastructure.Repositories.Demo;

/// <summary>Создаёт риски во всех колонках с согласованными оценками и пересмотрами</summary>
internal sealed partial class BankingDemoRepository : IBankingDemoRepository
{
    /// <summary>Допустимая точность продолжительности простоя демосценария</summary>
    private const int DowntimePrecision = 6;

    /// <summary>Сохраняет реестр и историю с действующими шкалами клиента</summary>
    /// <param name="data">Исходный набор</param>
    /// <param name="shift">Смещение дат</param>
    /// <param name="operation">Общая операция журнала</param>
    /// <param name="transaction">Общая транзакция</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task SeedRegistryAsync(BankingDemoDataSet data, TimeSpan shift, Guid operation,
        IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var scale = await EnsureScaleAsync(data, transaction, cancellationToken);
        var settingsQuery = $"SELECT * FROM {EntityMapper.TbName<RiskAcceptanceSettingsDal>()} WHERE {EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(RiskAcceptanceSettingsDal.Id))}";
        var settings = await connection.QuerySingleAsync<RiskAcceptanceSettingsDal>(new CommandDefinition(settingsQuery,
            new { Id = AssessmentMethod.AcceptanceSettingsId }, transaction, cancellationToken: cancellationToken));
        foreach (var source in data.Risks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceAssessment = data.Assessments.SingleOrDefault(item => item.RiskId == source.Id);
            var assessment = sourceAssessment is null ? null : CalculateAssessment(source, sourceAssessment, data, scale, settings, shift);
            var risk = source with
            {
                CreatedAt = source.CreatedAt + shift, UpdatedAt = source.UpdatedAt + shift,
                AcceptanceLevel = assessment is { IsActive: true } ? AssessmentCalculation.Route(assessment.RiskScore, settings) : null
            };
            await InsertAsync(risk, transaction, cancellationToken);
            var related = new List<(string Path, object? Before, object After)>();
            foreach (var item in data.RiskProcesses.Where(item => item.RiskId == risk.Id))
            {
                var link = item with { LinkedAt = item.LinkedAt + shift };
                await InsertAsync(link, transaction, cancellationToken);
                related.Add((BusinessHistory.ProcessesPath, null, link));
            }
            if (assessment is not null)
            {
                await InsertAsync(assessment, transaction, cancellationToken);
                related.Add((BusinessHistory.AssessmentsPath, null, assessment));
            }
            await BusinessHistory.RecordAsync<RiskDal>(transaction, operation, IdentityAccounts.SystemUserId,
                risk.Id, null, risk, system: true, relatedChanges: related, automaticSource: SeedSource);
        }
        foreach (var source in data.RiskComments)
        {
            var row = source with { CreatedAt = source.CreatedAt + shift, UpdatedAt = source.UpdatedAt + shift };
            await InsertAsync(row, transaction, cancellationToken);
            await BusinessHistory.RecordAsync<RiskCommentDal>(transaction, operation, IdentityAccounts.SystemUserId,
                row.Id, null, row, system: true, automaticSource: SeedSource);
        }
        foreach (var source in data.ReviewRuns)
        {
            await InsertAsync(source with { OccurredAt = source.OccurredAt + shift }, transaction, cancellationToken);
        }
        foreach (var source in data.Reviews)
        {
            await InsertAsync(source with { ProcessedAt = source.ProcessedAt + shift, StartedAt = source.StartedAt + shift }, transaction, cancellationToken);
        }
    }

    /// <summary>Пересчитывает демонстрационные входы существующим предметным калькулятором</summary>
    /// <param name="risk">Исходная карточка</param>
    /// <param name="source">Входы и метаданные оценки</param>
    /// <param name="data">Связанные объекты проекта</param>
    /// <param name="scale">Действующая шкала</param>
    /// <param name="settings">Настройки принятия клиента</param>
    /// <param name="shift">Смещение дат</param>
    /// <returns>Воспроизводимая оценка с точной версией шкалы</returns>
    private static RiskAssessmentDal CalculateAssessment(RiskDal risk, RiskAssessmentDal source, BankingDemoDataSet data,
        ImpactScale scale, RiskAcceptanceSettingsDal settings, TimeSpan shift)
    {
        var rate = data.Assets.SingleOrDefault(item => item.Id == risk.AssetId)?.HourlyDowntimeCost;
        var inputs = new AssessmentRequest
        {
            LikelihoodLevelId = source.LikelihoodLevelId,
            LossAmount = rate is null ? source.LossAmount : null,
            DowntimeHours = rate is decimal cost && source.LossAmount is decimal loss
                ? decimal.Round(loss / cost, DowntimePrecision) : null
        };
        var calculated = AssessmentCalculation.Calculate(risk.Id, inputs, rate, scale);
        // Не создаём принятый риск с уровнем TREATMENT_REQUIRED при необычных границах клиента.
        if (risk.Status == RiskRules.Accepted && AssessmentCalculation.Route(calculated.RiskScore, settings) == RiskRules.TreatmentRequired)
        {
            inputs = inputs with { LikelihoodLevelId = AssessmentMethod.Describe().LikelihoodLevels[0].Id };
            calculated = AssessmentCalculation.Calculate(risk.Id, inputs, rate, scale);
        }
        return calculated with
        {
            Id = source.Id, Version = source.Version, CreatedById = source.CreatedById,
            IsActive = source.IsActive, CreatedAt = source.CreatedAt + shift, ResponseMode = source.ResponseMode
        };
    }

    /// <summary>Сохраняет существующую шкалу либо публикует начальную при её отсутствии</summary>
    /// <param name="data">Границы демонстрационного набора</param>
    /// <param name="transaction">Общая транзакция</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    /// <returns>Шкала для расчёта демонстрационных оценок</returns>
    private async Task<ImpactScale> EnsureScaleAsync(BankingDemoDataSet data, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var versionQuery = $"SELECT * FROM {EntityMapper.TbName<ImpactScaleDal>()} ORDER BY {EntityMapper.ColName<ImpactScaleDal>(x => x.Sequence)} DESC LIMIT 1";
        var version = await connection.QuerySingleOrDefaultAsync<ImpactScaleDal>(new CommandDefinition(versionQuery,
            transaction: transaction, cancellationToken: cancellationToken));
        if (version is null)
        {
            version = new ImpactScaleDal { Id = data.ScaleId, CreatedById = IdentityAccounts.SystemUserId, CreatedAt = DateTime.UtcNow };
            await InsertAsync(version, transaction, cancellationToken);
            foreach (var threshold in AssessmentCalculation.BuildThresholds(version.Id, data.ScaleMinimums))
            {
                await InsertAsync(threshold, transaction, cancellationToken);
            }
        }
        var thresholdsQuery = $"SELECT * FROM {EntityMapper.TbName<ImpactThresholdDal>()} WHERE {EntityMapper.ColName<ImpactThresholdDal>(x => x.ScaleId)} = {EntityMapper.ParameterName(nameof(ImpactThresholdDal.ScaleId))} ORDER BY {EntityMapper.ColName<ImpactThresholdDal>(x => x.Weight)}";
        var thresholds = await connection.QueryAsync<ImpactThresholdDal>(new CommandDefinition(thresholdsQuery,
            new { ScaleId = version.Id }, transaction, cancellationToken: cancellationToken));
        return new ImpactScale(version, thresholds.ToArray());
    }
}
