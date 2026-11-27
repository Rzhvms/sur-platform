using Infrastructure.Repositories.History.Helpers;
using System.Data;
using Application.Constants;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Repositories.Demo.Interfaces;
using Infrastructure.Repositories.Demo.Models;

namespace Infrastructure.Repositories.Demo;

/// <summary>Добавляет ручные меры, исполнителей и снимки согласования</summary>
internal sealed partial class BankingDemoRepository : IBankingDemoRepository
{
    /// <summary>Интервал между подачей и историческим согласованием меры</summary>
    private const int ApprovalDelayDays = 1;

    /// <summary>Сохраняет меры без создания поручений во внешние системы</summary>
    /// <param name="data">Исходный набор</param>
    /// <param name="shift">Смещение дат</param>
    /// <param name="operation">Общая операция журнала</param>
    /// <param name="transaction">Общая транзакция</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task SeedTreatmentsAsync(BankingDemoDataSet data, TimeSpan shift, Guid operation,
        IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var settingsQuery = $"SELECT * FROM {EntityMapper.TbName<TreatmentApprovalSettingsDal>()}";
        var settings = await connection.QuerySingleAsync<TreatmentApprovalSettingsDal>(new CommandDefinition(settingsQuery,
            transaction: transaction, cancellationToken: cancellationToken));
        var ceoQuery = $"""
            SELECT p.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)}
            FROM {EntityMapper.TbName<ResponsibilityDal>()} r
            JOIN {EntityMapper.TbName<OrgPersonDal>()} p ON p.{EntityMapper.ColName<OrgPersonDal>(x => x.Id)} = r.{EntityMapper.ColName<ResponsibilityDal>(x => x.PersonId)}
            WHERE r.{EntityMapper.ColName<ResponsibilityDal>(x => x.IsActive)}
                AND r.{EntityMapper.ColName<ResponsibilityDal>(x => x.ResponsibilityType)} = {EntityMapper.ParameterName(nameof(ResponsibilityDal.ResponsibilityType))}
            """;
        var ceoUserId = await connection.QuerySingleAsync<Guid>(new CommandDefinition(ceoQuery,
            new { ResponsibilityType = OrganizationRoles.Ceo }, transaction, cancellationToken: cancellationToken));
        foreach (var source in data.Treatments)
        {
            var cost = source.Status is TreatmentRules.Pending or TreatmentRules.Approved
                ? Math.Max(source.CostPlanned, settings.OwnerThreshold) : source.CostPlanned;
            var row = source with
            {
                CostPlanned = cost, CreatedAt = source.CreatedAt + shift, UpdatedAt = source.UpdatedAt + shift,
                DueDate = DateTime.SpecifyKind((source.DueDate + shift).Date, DateTimeKind.Unspecified)
            };
            await InsertAsync(row, transaction, cancellationToken);
            await BusinessHistory.RecordAsync<TreatmentDal>(transaction, operation, IdentityAccounts.SystemUserId,
                row.Id, null, row, system: true, automaticSource: SeedSource);
            if (row.Status != TreatmentRules.Draft && row.Status != TreatmentRules.Cancelled && row.CostPlanned >= settings.OwnerThreshold)
            {
                var level = row.CostPlanned >= settings.CeoThreshold ? OrganizationRoles.Ceo
                    : row.CostPlanned >= settings.DirectorThreshold ? OrganizationRoles.Director : OrganizationRoles.Owner;
                var directorateId = data.Processes.Single(item => item.Id == row.ProcessId).DirectorateId;
                var director = data.Responsibilities.Single(item => item.ResponsibilityType == OrganizationRoles.Director && item.DirectorateId == directorateId);
                var directorUserId = data.Persons.Single(item => item.Id == director.PersonId).UserId;
                var approver = level == OrganizationRoles.Ceo ? ceoUserId
                    : level == OrganizationRoles.Director ? directorUserId : row.CreatedById;
                var approval = new TreatmentApprovalDal
                {
                    Id = row.Id, Version = row.Version, TreatmentId = row.Id,
                    Status = row.Status == TreatmentRules.Pending ? TreatmentRules.Pending : TreatmentRules.Approved,
                    RequiredLevel = level, CostPlanned = row.CostPlanned,
                    TriggerThreshold = level == OrganizationRoles.Ceo ? settings.CeoThreshold
                        : level == OrganizationRoles.Director ? settings.DirectorThreshold : settings.OwnerThreshold,
                    OwnerThreshold = settings.OwnerThreshold, DirectorThreshold = settings.DirectorThreshold, CeoThreshold = settings.CeoThreshold,
                    RequestedById = row.CreatedById, RequestedAt = row.CreatedAt,
                    DecidedById = row.Status == TreatmentRules.Pending ? null : approver,
                    DecidedAt = row.Status == TreatmentRules.Pending ? null : row.CreatedAt.AddDays(ApprovalDelayDays)
                };
                await InsertAsync(approval, transaction, cancellationToken);
            }
        }
        foreach (var source in data.Assignments)
        {
            await InsertAsync(source with { AssignedAt = source.AssignedAt + shift }, transaction, cancellationToken);
        }
        foreach (var source in data.TreatmentComments)
        {
            await InsertAsync(source with { CreatedAt = source.CreatedAt + shift, UpdatedAt = source.UpdatedAt + shift }, transaction, cancellationToken);
        }
    }
}
