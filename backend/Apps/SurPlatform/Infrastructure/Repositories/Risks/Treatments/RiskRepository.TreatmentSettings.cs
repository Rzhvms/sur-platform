using System.Data;
using Infrastructure.Migrations;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.UseCases.Treatments.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Repositories.Risks.Helpers;

namespace Infrastructure.Repositories.Risks;

/// <summary>Настройка порогов согласования мер</summary>
internal sealed partial class RiskRepository
{
    /// <inheritdoc/>
    public async Task<TreatmentApprovalSettingsDal> GetTreatmentSettingsAsync(RiskActor actor)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, IdentityRights.AdminCode, transaction);
        var settings = await ReadTreatmentSettingsAsync(transaction);
        transaction.Commit();
        return settings;
    }

    /// <inheritdoc/>
    public Task<TreatmentApprovalSettingsDal> SaveTreatmentSettingsAsync(RiskActor actor, TreatmentThresholdsRequest request)
    {
        return WriteAsync(actor, IdentityRights.AdminCode, async transaction =>
        {
            var previous = await ReadTreatmentSettingsAsync(transaction);
            RequireVersion(previous.Version, request.Version);
            if (request.OwnerThreshold is not decimal owner || request.DirectorThreshold is not decimal director
                || request.CeoThreshold is not decimal ceo || owner >= director || director >= ceo)
            {
                throw new IdentityCommandException(400, TreatmentRules.Invalid);
            }
            ValidateTreatmentCost(owner);
            ValidateTreatmentCost(director);
            ValidateTreatmentCost(ceo);
            if (previous.OwnerThreshold == owner && previous.DirectorThreshold == director && previous.CeoThreshold == ceo)
            {
                return previous;
            }
            var query = $"""
                UPDATE {EntityMapper.TbName<TreatmentApprovalSettingsDal>()} SET {EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.OwnerThreshold)} = {EntityMapper.ParameterName(nameof(owner))},
                    {EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.DirectorThreshold)} = {EntityMapper.ParameterName(nameof(director))}, {EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.CeoThreshold)} = {EntityMapper.ParameterName(nameof(ceo))},
                    {EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.Version)} = gen_random_uuid()
                WHERE {EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(previous.Id))} AND {EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(previous.Version))} RETURNING *
                """;
            var settings = await connection.QuerySingleOrDefaultAsync<TreatmentApprovalSettingsDal>(query,
                new { previous.Id, previous.Version, owner, director, ceo }, transaction)
                ?? throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
            await BusinessHistory.RecordAsync(transaction, Guid.NewGuid(), actor.UserId, settings.Id, previous, settings);
            return settings;
        });
    }

    /// <summary>Читает единственную действующую настройку клиента</summary>
    /// <param name="transaction">Согласованный снимок</param>
    /// <returns>Действующие пороги</returns>
    private Task<TreatmentApprovalSettingsDal> ReadTreatmentSettingsAsync(IDbTransaction transaction)
    {
        var id = Guid.Parse(Date_202611140500_AddTreatments.SettingsId);
        var query = $"SELECT * FROM {EntityMapper.TbName<TreatmentApprovalSettingsDal>()} WHERE {EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))}";
        return connection.QuerySingleAsync<TreatmentApprovalSettingsDal>(query, new { id }, transaction);
    }

    /// <summary>Выбирает самый высокий достигнутый порог</summary>
    /// <param name="cost">Плановая стоимость</param>
    /// <param name="settings">Текущая настройка</param>
    /// <returns>Уровень согласования или null для прямой передачи</returns>
    private static string? ApprovalLevel(decimal cost, TreatmentApprovalSettingsDal settings)
    {
        if (cost >= settings.CeoThreshold)
        {
            return OrganizationRoles.Ceo;
        }
        if (cost >= settings.DirectorThreshold)
        {
            return OrganizationRoles.Director;
        }
        return cost >= settings.OwnerThreshold ? OrganizationRoles.Owner : null;
    }
}
