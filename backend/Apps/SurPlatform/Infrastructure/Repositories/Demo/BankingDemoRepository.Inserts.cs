using System.Data;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Repositories.Demo.Interfaces;

namespace Infrastructure.Repositories.Demo;

/// <summary>Параметризованные операции вставки демонстрационных сущностей</summary>
internal sealed partial class BankingDemoRepository : IBankingDemoRepository
{
    /// <summary>Добавляет новую запись UserProfileDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(UserProfileDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<UserProfileDal>()}
                ({EntityMapper.ColName<UserProfileDal>(x => x.Id)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.UserId)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.FirstName)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.LastName)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.Patronymic)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.Phone)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.Email)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.NormalizedEmail)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.IsActive)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.IsEmailConfirmed)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.IsPhoneConfirmed)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.LockoutUntil)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.CreatedAt)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.UpdatedAt)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.FullName)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.Version)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.AccessVersion)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.MustChangePassword)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.TemporaryPasswordExpiresAt)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.InvitationStatus)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.InvitationVersion)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.InvitationStartedAt)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.UpdatedBy)},
                 {EntityMapper.ColName<UserProfileDal>(x => x.IsFirstAdministrator)})
            VALUES
                ({EntityMapper.ParameterName(nameof(UserProfileDal.Id))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.UserId))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.FirstName))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.LastName))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.Patronymic))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.Phone))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.Email))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.NormalizedEmail))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.IsActive))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.IsEmailConfirmed))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.IsPhoneConfirmed))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.LockoutUntil))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.CreatedAt))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.UpdatedAt))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.FullName))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.Version))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.AccessVersion))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.MustChangePassword))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.TemporaryPasswordExpiresAt))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.InvitationStatus))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.InvitationVersion))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.InvitationStartedAt))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.UpdatedBy))},
                 {EntityMapper.ParameterName(nameof(UserProfileDal.IsFirstAdministrator))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись DirectorateDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(DirectorateDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<DirectorateDal>()}
                ({EntityMapper.ColName<DirectorateDal>(x => x.Id)},
                 {EntityMapper.ColName<DirectorateDal>(x => x.Version)},
                 {EntityMapper.ColName<DirectorateDal>(x => x.IsActive)},
                 {EntityMapper.ColName<DirectorateDal>(x => x.CreatedAt)},
                 {EntityMapper.ColName<DirectorateDal>(x => x.UpdatedAt)},
                 {EntityMapper.ColName<DirectorateDal>(x => x.Name)})
            VALUES
                ({EntityMapper.ParameterName(nameof(DirectorateDal.Id))},
                 {EntityMapper.ParameterName(nameof(DirectorateDal.Version))},
                 {EntityMapper.ParameterName(nameof(DirectorateDal.IsActive))},
                 {EntityMapper.ParameterName(nameof(DirectorateDal.CreatedAt))},
                 {EntityMapper.ParameterName(nameof(DirectorateDal.UpdatedAt))},
                 {EntityMapper.ParameterName(nameof(DirectorateDal.Name))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись OrgPersonDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(OrgPersonDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<OrgPersonDal>()}
                ({EntityMapper.ColName<OrgPersonDal>(x => x.Id)},
                 {EntityMapper.ColName<OrgPersonDal>(x => x.Version)},
                 {EntityMapper.ColName<OrgPersonDal>(x => x.IsActive)},
                 {EntityMapper.ColName<OrgPersonDal>(x => x.CreatedAt)},
                 {EntityMapper.ColName<OrgPersonDal>(x => x.UpdatedAt)},
                 {EntityMapper.ColName<OrgPersonDal>(x => x.UserId)},
                 {EntityMapper.ColName<OrgPersonDal>(x => x.DirectorateId)},
                 {EntityMapper.ColName<OrgPersonDal>(x => x.PositionName)})
            VALUES
                ({EntityMapper.ParameterName(nameof(OrgPersonDal.Id))},
                 {EntityMapper.ParameterName(nameof(OrgPersonDal.Version))},
                 {EntityMapper.ParameterName(nameof(OrgPersonDal.IsActive))},
                 {EntityMapper.ParameterName(nameof(OrgPersonDal.CreatedAt))},
                 {EntityMapper.ParameterName(nameof(OrgPersonDal.UpdatedAt))},
                 {EntityMapper.ParameterName(nameof(OrgPersonDal.UserId))},
                 {EntityMapper.ParameterName(nameof(OrgPersonDal.DirectorateId))},
                 {EntityMapper.ParameterName(nameof(OrgPersonDal.PositionName))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись ProcessDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(ProcessDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<ProcessDal>()}
                ({EntityMapper.ColName<ProcessDal>(x => x.Id)},
                 {EntityMapper.ColName<ProcessDal>(x => x.Version)},
                 {EntityMapper.ColName<ProcessDal>(x => x.IsActive)},
                 {EntityMapper.ColName<ProcessDal>(x => x.CreatedAt)},
                 {EntityMapper.ColName<ProcessDal>(x => x.UpdatedAt)},
                 {EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)},
                 {EntityMapper.ColName<ProcessDal>(x => x.OwnerId)},
                 {EntityMapper.ColName<ProcessDal>(x => x.Name)},
                 {EntityMapper.ColName<ProcessDal>(x => x.Description)})
            VALUES
                ({EntityMapper.ParameterName(nameof(ProcessDal.Id))},
                 {EntityMapper.ParameterName(nameof(ProcessDal.Version))},
                 {EntityMapper.ParameterName(nameof(ProcessDal.IsActive))},
                 {EntityMapper.ParameterName(nameof(ProcessDal.CreatedAt))},
                 {EntityMapper.ParameterName(nameof(ProcessDal.UpdatedAt))},
                 {EntityMapper.ParameterName(nameof(ProcessDal.DirectorateId))},
                 {EntityMapper.ParameterName(nameof(ProcessDal.OwnerId))},
                 {EntityMapper.ParameterName(nameof(ProcessDal.Name))},
                 {EntityMapper.ParameterName(nameof(ProcessDal.Description))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись AssetDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(AssetDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<AssetDal>()}
                ({EntityMapper.ColName<AssetDal>(x => x.Id)},
                 {EntityMapper.ColName<AssetDal>(x => x.Version)},
                 {EntityMapper.ColName<AssetDal>(x => x.IsActive)},
                 {EntityMapper.ColName<AssetDal>(x => x.CreatedAt)},
                 {EntityMapper.ColName<AssetDal>(x => x.UpdatedAt)},
                 {EntityMapper.ColName<AssetDal>(x => x.Name)},
                 {EntityMapper.ColName<AssetDal>(x => x.Description)},
                 {EntityMapper.ColName<AssetDal>(x => x.InventoryNumber)},
                 {EntityMapper.ColName<AssetDal>(x => x.HourlyDowntimeCost)})
            VALUES
                ({EntityMapper.ParameterName(nameof(AssetDal.Id))},
                 {EntityMapper.ParameterName(nameof(AssetDal.Version))},
                 {EntityMapper.ParameterName(nameof(AssetDal.IsActive))},
                 {EntityMapper.ParameterName(nameof(AssetDal.CreatedAt))},
                 {EntityMapper.ParameterName(nameof(AssetDal.UpdatedAt))},
                 {EntityMapper.ParameterName(nameof(AssetDal.Name))},
                 {EntityMapper.ParameterName(nameof(AssetDal.Description))},
                 {EntityMapper.ParameterName(nameof(AssetDal.InventoryNumber))},
                 {EntityMapper.ParameterName(nameof(AssetDal.HourlyDowntimeCost))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись HazardGroupDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(HazardGroupDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<HazardGroupDal>()}
                ({EntityMapper.ColName<HazardGroupDal>(x => x.Id)},
                 {EntityMapper.ColName<HazardGroupDal>(x => x.Version)},
                 {EntityMapper.ColName<HazardGroupDal>(x => x.Name)},
                 {EntityMapper.ColName<HazardGroupDal>(x => x.Description)},
                 {EntityMapper.ColName<HazardGroupDal>(x => x.IsActive)})
            VALUES
                ({EntityMapper.ParameterName(nameof(HazardGroupDal.Id))},
                 {EntityMapper.ParameterName(nameof(HazardGroupDal.Version))},
                 {EntityMapper.ParameterName(nameof(HazardGroupDal.Name))},
                 {EntityMapper.ParameterName(nameof(HazardGroupDal.Description))},
                 {EntityMapper.ParameterName(nameof(HazardGroupDal.IsActive))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись HazardDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(HazardDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<HazardDal>()}
                ({EntityMapper.ColName<HazardDal>(x => x.Id)},
                 {EntityMapper.ColName<HazardDal>(x => x.Version)},
                 {EntityMapper.ColName<HazardDal>(x => x.Name)},
                 {EntityMapper.ColName<HazardDal>(x => x.Description)},
                 {EntityMapper.ColName<HazardDal>(x => x.IsActive)},
                 {EntityMapper.ColName<HazardDal>(x => x.GroupId)})
            VALUES
                ({EntityMapper.ParameterName(nameof(HazardDal.Id))},
                 {EntityMapper.ParameterName(nameof(HazardDal.Version))},
                 {EntityMapper.ParameterName(nameof(HazardDal.Name))},
                 {EntityMapper.ParameterName(nameof(HazardDal.Description))},
                 {EntityMapper.ParameterName(nameof(HazardDal.IsActive))},
                 {EntityMapper.ParameterName(nameof(HazardDal.GroupId))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись RiskCategoryDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(RiskCategoryDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<RiskCategoryDal>()}
                ({EntityMapper.ColName<RiskCategoryDal>(x => x.Id)},
                 {EntityMapper.ColName<RiskCategoryDal>(x => x.Version)},
                 {EntityMapper.ColName<RiskCategoryDal>(x => x.Name)},
                 {EntityMapper.ColName<RiskCategoryDal>(x => x.Description)},
                 {EntityMapper.ColName<RiskCategoryDal>(x => x.IsActive)})
            VALUES
                ({EntityMapper.ParameterName(nameof(RiskCategoryDal.Id))},
                 {EntityMapper.ParameterName(nameof(RiskCategoryDal.Version))},
                 {EntityMapper.ParameterName(nameof(RiskCategoryDal.Name))},
                 {EntityMapper.ParameterName(nameof(RiskCategoryDal.Description))},
                 {EntityMapper.ParameterName(nameof(RiskCategoryDal.IsActive))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись ResponsibilityDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(ResponsibilityDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<ResponsibilityDal>()}
                ({EntityMapper.ColName<ResponsibilityDal>(x => x.Id)},
                 {EntityMapper.ColName<ResponsibilityDal>(x => x.Version)},
                 {EntityMapper.ColName<ResponsibilityDal>(x => x.IsActive)},
                 {EntityMapper.ColName<ResponsibilityDal>(x => x.CreatedAt)},
                 {EntityMapper.ColName<ResponsibilityDal>(x => x.UpdatedAt)},
                 {EntityMapper.ColName<ResponsibilityDal>(x => x.PersonId)},
                 {EntityMapper.ColName<ResponsibilityDal>(x => x.DirectorateId)},
                 {EntityMapper.ColName<ResponsibilityDal>(x => x.ResponsibilityType)})
            VALUES
                ({EntityMapper.ParameterName(nameof(ResponsibilityDal.Id))},
                 {EntityMapper.ParameterName(nameof(ResponsibilityDal.Version))},
                 {EntityMapper.ParameterName(nameof(ResponsibilityDal.IsActive))},
                 {EntityMapper.ParameterName(nameof(ResponsibilityDal.CreatedAt))},
                 {EntityMapper.ParameterName(nameof(ResponsibilityDal.UpdatedAt))},
                 {EntityMapper.ParameterName(nameof(ResponsibilityDal.PersonId))},
                 {EntityMapper.ParameterName(nameof(ResponsibilityDal.DirectorateId))},
                 {EntityMapper.ParameterName(nameof(ResponsibilityDal.ResponsibilityType))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись RiskDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(RiskDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<RiskDal>()}
                ({EntityMapper.ColName<RiskDal>(x => x.Id)},
                 {EntityMapper.ColName<RiskDal>(x => x.Version)},
                 {EntityMapper.ColName<RiskDal>(x => x.Name)},
                 {EntityMapper.ColName<RiskDal>(x => x.Description)},
                 {EntityMapper.ColName<RiskDal>(x => x.HazardId)},
                 {EntityMapper.ColName<RiskDal>(x => x.CategoryId)},
                 {EntityMapper.ColName<RiskDal>(x => x.AuthorId)},
                 {EntityMapper.ColName<RiskDal>(x => x.AssetId)},
                 {EntityMapper.ColName<RiskDal>(x => x.Status)},
                 {EntityMapper.ColName<RiskDal>(x => x.AcceptanceLevel)},
                 {EntityMapper.ColName<RiskDal>(x => x.CloseReason)},
                 {EntityMapper.ColName<RiskDal>(x => x.CreatedAt)},
                 {EntityMapper.ColName<RiskDal>(x => x.UpdatedAt)})
            VALUES
                ({EntityMapper.ParameterName(nameof(RiskDal.Id))},
                 {EntityMapper.ParameterName(nameof(RiskDal.Version))},
                 {EntityMapper.ParameterName(nameof(RiskDal.Name))},
                 {EntityMapper.ParameterName(nameof(RiskDal.Description))},
                 {EntityMapper.ParameterName(nameof(RiskDal.HazardId))},
                 {EntityMapper.ParameterName(nameof(RiskDal.CategoryId))},
                 {EntityMapper.ParameterName(nameof(RiskDal.AuthorId))},
                 {EntityMapper.ParameterName(nameof(RiskDal.AssetId))},
                 {EntityMapper.ParameterName(nameof(RiskDal.Status))},
                 {EntityMapper.ParameterName(nameof(RiskDal.AcceptanceLevel))},
                 {EntityMapper.ParameterName(nameof(RiskDal.CloseReason))},
                 {EntityMapper.ParameterName(nameof(RiskDal.CreatedAt))},
                 {EntityMapper.ParameterName(nameof(RiskDal.UpdatedAt))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись RiskProcessDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(RiskProcessDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<RiskProcessDal>()}
                ({EntityMapper.ColName<RiskProcessDal>(x => x.Id)},
                 {EntityMapper.ColName<RiskProcessDal>(x => x.Version)},
                 {EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)},
                 {EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)},
                 {EntityMapper.ColName<RiskProcessDal>(x => x.OwnerId)},
                 {EntityMapper.ColName<RiskProcessDal>(x => x.IsPrimary)},
                 {EntityMapper.ColName<RiskProcessDal>(x => x.LinkedAt)},
                 {EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)})
            VALUES
                ({EntityMapper.ParameterName(nameof(RiskProcessDal.Id))},
                 {EntityMapper.ParameterName(nameof(RiskProcessDal.Version))},
                 {EntityMapper.ParameterName(nameof(RiskProcessDal.RiskId))},
                 {EntityMapper.ParameterName(nameof(RiskProcessDal.ProcessId))},
                 {EntityMapper.ParameterName(nameof(RiskProcessDal.OwnerId))},
                 {EntityMapper.ParameterName(nameof(RiskProcessDal.IsPrimary))},
                 {EntityMapper.ParameterName(nameof(RiskProcessDal.LinkedAt))},
                 {EntityMapper.ParameterName(nameof(RiskProcessDal.UnlinkedAt))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись RiskAssessmentDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(RiskAssessmentDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<RiskAssessmentDal>()}
                ({EntityMapper.ColName<RiskAssessmentDal>(x => x.Id)},
                 {EntityMapper.ColName<RiskAssessmentDal>(x => x.Version)},
                 {EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskId)},
                 {EntityMapper.ColName<RiskAssessmentDal>(x => x.LikelihoodLevelId)},
                 {EntityMapper.ColName<RiskAssessmentDal>(x => x.ImpactLevelId)},
                 {EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskScore)},
                 {EntityMapper.ColName<RiskAssessmentDal>(x => x.IsActive)},
                 {EntityMapper.ColName<RiskAssessmentDal>(x => x.CreatedAt)},
                 {EntityMapper.ColName<RiskAssessmentDal>(x => x.ResponseMode)},
                 {EntityMapper.ColName<RiskAssessmentDal>(x => x.MethodId)},
                 {EntityMapper.ColName<RiskAssessmentDal>(x => x.ScaleVersionId)},
                 {EntityMapper.ColName<RiskAssessmentDal>(x => x.CreatedById)},
                 {EntityMapper.ColName<RiskAssessmentDal>(x => x.LossAmount)},
                 {EntityMapper.ColName<RiskAssessmentDal>(x => x.DowntimeWeeks)},
                 {EntityMapper.ColName<RiskAssessmentDal>(x => x.DowntimeDays)},
                 {EntityMapper.ColName<RiskAssessmentDal>(x => x.DowntimeHours)},
                 {EntityMapper.ColName<RiskAssessmentDal>(x => x.DowntimeTotalHours)},
                 {EntityMapper.ColName<RiskAssessmentDal>(x => x.HourlyDowntimeCostSnapshot)})
            VALUES
                ({EntityMapper.ParameterName(nameof(RiskAssessmentDal.Id))},
                 {EntityMapper.ParameterName(nameof(RiskAssessmentDal.Version))},
                 {EntityMapper.ParameterName(nameof(RiskAssessmentDal.RiskId))},
                 {EntityMapper.ParameterName(nameof(RiskAssessmentDal.LikelihoodLevelId))},
                 {EntityMapper.ParameterName(nameof(RiskAssessmentDal.ImpactLevelId))},
                 {EntityMapper.ParameterName(nameof(RiskAssessmentDal.RiskScore))},
                 {EntityMapper.ParameterName(nameof(RiskAssessmentDal.IsActive))},
                 {EntityMapper.ParameterName(nameof(RiskAssessmentDal.CreatedAt))},
                 {EntityMapper.ParameterName(nameof(RiskAssessmentDal.ResponseMode))},
                 {EntityMapper.ParameterName(nameof(RiskAssessmentDal.MethodId))},
                 {EntityMapper.ParameterName(nameof(RiskAssessmentDal.ScaleVersionId))},
                 {EntityMapper.ParameterName(nameof(RiskAssessmentDal.CreatedById))},
                 {EntityMapper.ParameterName(nameof(RiskAssessmentDal.LossAmount))},
                 {EntityMapper.ParameterName(nameof(RiskAssessmentDal.DowntimeWeeks))},
                 {EntityMapper.ParameterName(nameof(RiskAssessmentDal.DowntimeDays))},
                 {EntityMapper.ParameterName(nameof(RiskAssessmentDal.DowntimeHours))},
                 {EntityMapper.ParameterName(nameof(RiskAssessmentDal.DowntimeTotalHours))},
                 {EntityMapper.ParameterName(nameof(RiskAssessmentDal.HourlyDowntimeCostSnapshot))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись TreatmentDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(TreatmentDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<TreatmentDal>()}
                ({EntityMapper.ColName<TreatmentDal>(x => x.Id)},
                 {EntityMapper.ColName<TreatmentDal>(x => x.Version)},
                 {EntityMapper.ColName<TreatmentDal>(x => x.RiskId)},
                 {EntityMapper.ColName<TreatmentDal>(x => x.ProcessId)},
                 {EntityMapper.ColName<TreatmentDal>(x => x.RiskProcessId)},
                 {EntityMapper.ColName<TreatmentDal>(x => x.Title)},
                 {EntityMapper.ColName<TreatmentDal>(x => x.Description)},
                 {EntityMapper.ColName<TreatmentDal>(x => x.DueDate)},
                 {EntityMapper.ColName<TreatmentDal>(x => x.CostPlanned)},
                 {EntityMapper.ColName<TreatmentDal>(x => x.CostActual)},
                 {EntityMapper.ColName<TreatmentDal>(x => x.Status)},
                 {EntityMapper.ColName<TreatmentDal>(x => x.CreatedById)},
                 {EntityMapper.ColName<TreatmentDal>(x => x.CreatedAt)},
                 {EntityMapper.ColName<TreatmentDal>(x => x.UpdatedAt)})
            VALUES
                ({EntityMapper.ParameterName(nameof(TreatmentDal.Id))},
                 {EntityMapper.ParameterName(nameof(TreatmentDal.Version))},
                 {EntityMapper.ParameterName(nameof(TreatmentDal.RiskId))},
                 {EntityMapper.ParameterName(nameof(TreatmentDal.ProcessId))},
                 {EntityMapper.ParameterName(nameof(TreatmentDal.RiskProcessId))},
                 {EntityMapper.ParameterName(nameof(TreatmentDal.Title))},
                 {EntityMapper.ParameterName(nameof(TreatmentDal.Description))},
                 {EntityMapper.ParameterName(nameof(TreatmentDal.DueDate))},
                 {EntityMapper.ParameterName(nameof(TreatmentDal.CostPlanned))},
                 {EntityMapper.ParameterName(nameof(TreatmentDal.CostActual))},
                 {EntityMapper.ParameterName(nameof(TreatmentDal.Status))},
                 {EntityMapper.ParameterName(nameof(TreatmentDal.CreatedById))},
                 {EntityMapper.ParameterName(nameof(TreatmentDal.CreatedAt))},
                 {EntityMapper.ParameterName(nameof(TreatmentDal.UpdatedAt))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись TreatmentAssignmentDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(TreatmentAssignmentDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<TreatmentAssignmentDal>()}
                ({EntityMapper.ColName<TreatmentAssignmentDal>(x => x.Id)},
                 {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.TreatmentId)},
                 {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssigneeId)},
                 {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignedById)},
                 {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignedAt)},
                 {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignmentSource)})
            VALUES
                ({EntityMapper.ParameterName(nameof(TreatmentAssignmentDal.Id))},
                 {EntityMapper.ParameterName(nameof(TreatmentAssignmentDal.TreatmentId))},
                 {EntityMapper.ParameterName(nameof(TreatmentAssignmentDal.AssigneeId))},
                 {EntityMapper.ParameterName(nameof(TreatmentAssignmentDal.AssignedById))},
                 {EntityMapper.ParameterName(nameof(TreatmentAssignmentDal.AssignedAt))},
                 {EntityMapper.ParameterName(nameof(TreatmentAssignmentDal.AssignmentSource))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись RiskCommentDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(RiskCommentDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<RiskCommentDal>()}
                ({EntityMapper.ColName<RiskCommentDal>(x => x.Id)},
                 {EntityMapper.ColName<RiskCommentDal>(x => x.Version)},
                 {EntityMapper.ColName<RiskCommentDal>(x => x.RiskId)},
                 {EntityMapper.ColName<RiskCommentDal>(x => x.AuthorId)},
                 {EntityMapper.ColName<RiskCommentDal>(x => x.Text)},
                 {EntityMapper.ColName<RiskCommentDal>(x => x.IsSystem)},
                 {EntityMapper.ColName<RiskCommentDal>(x => x.IsDeleted)},
                 {EntityMapper.ColName<RiskCommentDal>(x => x.DeletedAt)},
                 {EntityMapper.ColName<RiskCommentDal>(x => x.DeletedById)},
                 {EntityMapper.ColName<RiskCommentDal>(x => x.CreatedAt)},
                 {EntityMapper.ColName<RiskCommentDal>(x => x.UpdatedAt)})
            VALUES
                ({EntityMapper.ParameterName(nameof(RiskCommentDal.Id))},
                 {EntityMapper.ParameterName(nameof(RiskCommentDal.Version))},
                 {EntityMapper.ParameterName(nameof(RiskCommentDal.RiskId))},
                 {EntityMapper.ParameterName(nameof(RiskCommentDal.AuthorId))},
                 {EntityMapper.ParameterName(nameof(RiskCommentDal.Text))},
                 {EntityMapper.ParameterName(nameof(RiskCommentDal.IsSystem))},
                 {EntityMapper.ParameterName(nameof(RiskCommentDal.IsDeleted))},
                 {EntityMapper.ParameterName(nameof(RiskCommentDal.DeletedAt))},
                 {EntityMapper.ParameterName(nameof(RiskCommentDal.DeletedById))},
                 {EntityMapper.ParameterName(nameof(RiskCommentDal.CreatedAt))},
                 {EntityMapper.ParameterName(nameof(RiskCommentDal.UpdatedAt))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись TreatmentCommentDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(TreatmentCommentDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<TreatmentCommentDal>()}
                ({EntityMapper.ColName<TreatmentCommentDal>(x => x.Id)},
                 {EntityMapper.ColName<TreatmentCommentDal>(x => x.Version)},
                 {EntityMapper.ColName<TreatmentCommentDal>(x => x.TreatmentId)},
                 {EntityMapper.ColName<TreatmentCommentDal>(x => x.AuthorId)},
                 {EntityMapper.ColName<TreatmentCommentDal>(x => x.Text)},
                 {EntityMapper.ColName<TreatmentCommentDal>(x => x.CreatedAt)},
                 {EntityMapper.ColName<TreatmentCommentDal>(x => x.UpdatedAt)},
                 {EntityMapper.ColName<TreatmentCommentDal>(x => x.IsDeleted)},
                 {EntityMapper.ColName<TreatmentCommentDal>(x => x.DeletedAt)},
                 {EntityMapper.ColName<TreatmentCommentDal>(x => x.DeletedById)})
            VALUES
                ({EntityMapper.ParameterName(nameof(TreatmentCommentDal.Id))},
                 {EntityMapper.ParameterName(nameof(TreatmentCommentDal.Version))},
                 {EntityMapper.ParameterName(nameof(TreatmentCommentDal.TreatmentId))},
                 {EntityMapper.ParameterName(nameof(TreatmentCommentDal.AuthorId))},
                 {EntityMapper.ParameterName(nameof(TreatmentCommentDal.Text))},
                 {EntityMapper.ParameterName(nameof(TreatmentCommentDal.CreatedAt))},
                 {EntityMapper.ParameterName(nameof(TreatmentCommentDal.UpdatedAt))},
                 {EntityMapper.ParameterName(nameof(TreatmentCommentDal.IsDeleted))},
                 {EntityMapper.ParameterName(nameof(TreatmentCommentDal.DeletedAt))},
                 {EntityMapper.ParameterName(nameof(TreatmentCommentDal.DeletedById))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись ReviewRunDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(ReviewRunDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<ReviewRunDal>()}
                ({EntityMapper.ColName<ReviewRunDal>(x => x.Id)},
                 {EntityMapper.ColName<ReviewRunDal>(x => x.Reason)},
                 {EntityMapper.ColName<ReviewRunDal>(x => x.SourceType)},
                 {EntityMapper.ColName<ReviewRunDal>(x => x.SourceId)},
                 {EntityMapper.ColName<ReviewRunDal>(x => x.SourceNameSnapshot)},
                 {EntityMapper.ColName<ReviewRunDal>(x => x.ExternalSystemNameSnapshot)},
                 {EntityMapper.ColName<ReviewRunDal>(x => x.InitiatedById)},
                 {EntityMapper.ColName<ReviewRunDal>(x => x.ConfigurationAuthorId)},
                 {EntityMapper.ColName<ReviewRunDal>(x => x.ResponsibleUserId)},
                 {EntityMapper.ColName<ReviewRunDal>(x => x.TimeZone)},
                 {EntityMapper.ColName<ReviewRunDal>(x => x.ScheduledDate)},
                 {EntityMapper.ColName<ReviewRunDal>(x => x.OccurredAt)},
                 {EntityMapper.ColName<ReviewRunDal>(x => x.Status)},
                 {EntityMapper.ColName<ReviewRunDal>(x => x.RawPayload)},
                 {EntityMapper.ColName<ReviewRunDal>(x => x.TargetRiskIds)})
            VALUES
                ({EntityMapper.ParameterName(nameof(ReviewRunDal.Id))},
                 {EntityMapper.ParameterName(nameof(ReviewRunDal.Reason))},
                 {EntityMapper.ParameterName(nameof(ReviewRunDal.SourceType))},
                 {EntityMapper.ParameterName(nameof(ReviewRunDal.SourceId))},
                 {EntityMapper.ParameterName(nameof(ReviewRunDal.SourceNameSnapshot))},
                 {EntityMapper.ParameterName(nameof(ReviewRunDal.ExternalSystemNameSnapshot))},
                 {EntityMapper.ParameterName(nameof(ReviewRunDal.InitiatedById))},
                 {EntityMapper.ParameterName(nameof(ReviewRunDal.ConfigurationAuthorId))},
                 {EntityMapper.ParameterName(nameof(ReviewRunDal.ResponsibleUserId))},
                 {EntityMapper.ParameterName(nameof(ReviewRunDal.TimeZone))},
                 {EntityMapper.ParameterName(nameof(ReviewRunDal.ScheduledDate))},
                 {EntityMapper.ParameterName(nameof(ReviewRunDal.OccurredAt))},
                 {EntityMapper.ParameterName(nameof(ReviewRunDal.Status))},
                 {EntityMapper.ParameterName(nameof(ReviewRunDal.RawPayload))},
                 {EntityMapper.ParameterName(nameof(ReviewRunDal.TargetRiskIds))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись RiskReviewDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(RiskReviewDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<RiskReviewDal>()}
                ({EntityMapper.ColName<RiskReviewDal>(x => x.Id)},
                 {EntityMapper.ColName<RiskReviewDal>(x => x.ReviewRunId)},
                 {EntityMapper.ColName<RiskReviewDal>(x => x.RiskId)},
                 {EntityMapper.ColName<RiskReviewDal>(x => x.Status)},
                 {EntityMapper.ColName<RiskReviewDal>(x => x.ProcessedAt)},
                 {EntityMapper.ColName<RiskReviewDal>(x => x.ErrorMessage)},
                 {EntityMapper.ColName<RiskReviewDal>(x => x.PreviousAssessmentId)},
                 {EntityMapper.ColName<RiskReviewDal>(x => x.NewAssessmentId)},
                 {EntityMapper.ColName<RiskReviewDal>(x => x.StartedAt)},
                 {EntityMapper.ColName<RiskReviewDal>(x => x.StartedById)},
                 {EntityMapper.ColName<RiskReviewDal>(x => x.CompletedAt)},
                 {EntityMapper.ColName<RiskReviewDal>(x => x.CompletedById)},
                 {EntityMapper.ColName<RiskReviewDal>(x => x.CloseReason)})
            VALUES
                ({EntityMapper.ParameterName(nameof(RiskReviewDal.Id))},
                 {EntityMapper.ParameterName(nameof(RiskReviewDal.ReviewRunId))},
                 {EntityMapper.ParameterName(nameof(RiskReviewDal.RiskId))},
                 {EntityMapper.ParameterName(nameof(RiskReviewDal.Status))},
                 {EntityMapper.ParameterName(nameof(RiskReviewDal.ProcessedAt))},
                 {EntityMapper.ParameterName(nameof(RiskReviewDal.ErrorMessage))},
                 {EntityMapper.ParameterName(nameof(RiskReviewDal.PreviousAssessmentId))},
                 {EntityMapper.ParameterName(nameof(RiskReviewDal.NewAssessmentId))},
                 {EntityMapper.ParameterName(nameof(RiskReviewDal.StartedAt))},
                 {EntityMapper.ParameterName(nameof(RiskReviewDal.StartedById))},
                 {EntityMapper.ParameterName(nameof(RiskReviewDal.CompletedAt))},
                 {EntityMapper.ParameterName(nameof(RiskReviewDal.CompletedById))},
                 {EntityMapper.ParameterName(nameof(RiskReviewDal.CloseReason))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись UserRoleDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(UserRoleDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<UserRoleDal>()}
                ({EntityMapper.ColName<UserRoleDal>(x => x.UserId)},
                 {EntityMapper.ColName<UserRoleDal>(x => x.RoleId)},
                 {EntityMapper.ColName<UserRoleDal>(x => x.IsActive)},
                 {EntityMapper.ColName<UserRoleDal>(x => x.UpdatedBy)},
                 {EntityMapper.ColName<UserRoleDal>(x => x.UpdatedAt)})
            VALUES
                ({EntityMapper.ParameterName(nameof(UserRoleDal.UserId))},
                 {EntityMapper.ParameterName(nameof(UserRoleDal.RoleId))},
                 {EntityMapper.ParameterName(nameof(UserRoleDal.IsActive))},
                 {EntityMapper.ParameterName(nameof(UserRoleDal.UpdatedBy))},
                 {EntityMapper.ParameterName(nameof(UserRoleDal.UpdatedAt))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись UserCredentialDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(UserCredentialDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<UserCredentialDal>()}
                ({EntityMapper.ColName<UserCredentialDal>(x => x.Id)},
                 {EntityMapper.ColName<UserCredentialDal>(x => x.UserId)},
                 {EntityMapper.ColName<UserCredentialDal>(x => x.Password)},
                 {EntityMapper.ColName<UserCredentialDal>(x => x.PasswordChangedAt)})
            VALUES
                ({EntityMapper.ParameterName(nameof(UserCredentialDal.Id))},
                 {EntityMapper.ParameterName(nameof(UserCredentialDal.UserId))},
                 {EntityMapper.ParameterName(nameof(UserCredentialDal.Password))},
                 {EntityMapper.ParameterName(nameof(UserCredentialDal.PasswordChangedAt))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись ImpactScaleDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(ImpactScaleDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<ImpactScaleDal>()}
                ({EntityMapper.ColName<ImpactScaleDal>(x => x.Id)},
                 {EntityMapper.ColName<ImpactScaleDal>(x => x.CreatedById)},
                 {EntityMapper.ColName<ImpactScaleDal>(x => x.CreatedAt)})
            VALUES
                ({EntityMapper.ParameterName(nameof(ImpactScaleDal.Id))},
                 {EntityMapper.ParameterName(nameof(ImpactScaleDal.CreatedById))},
                 {EntityMapper.ParameterName(nameof(ImpactScaleDal.CreatedAt))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись ImpactThresholdDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(ImpactThresholdDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<ImpactThresholdDal>()}
                ({EntityMapper.ColName<ImpactThresholdDal>(x => x.ScaleId)},
                 {EntityMapper.ColName<ImpactThresholdDal>(x => x.Weight)},
                 {EntityMapper.ColName<ImpactThresholdDal>(x => x.MinLossAmount)},
                 {EntityMapper.ColName<ImpactThresholdDal>(x => x.MaxLossAmount)})
            VALUES
                ({EntityMapper.ParameterName(nameof(ImpactThresholdDal.ScaleId))},
                 {EntityMapper.ParameterName(nameof(ImpactThresholdDal.Weight))},
                 {EntityMapper.ParameterName(nameof(ImpactThresholdDal.MinLossAmount))},
                 {EntityMapper.ParameterName(nameof(ImpactThresholdDal.MaxLossAmount))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись TreatmentApprovalDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(TreatmentApprovalDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<TreatmentApprovalDal>()}
                ({EntityMapper.ColName<TreatmentApprovalDal>(x => x.Id)},
                 {EntityMapper.ColName<TreatmentApprovalDal>(x => x.Version)},
                 {EntityMapper.ColName<TreatmentApprovalDal>(x => x.TreatmentId)},
                 {EntityMapper.ColName<TreatmentApprovalDal>(x => x.Status)},
                 {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RequiredLevel)},
                 {EntityMapper.ColName<TreatmentApprovalDal>(x => x.CostPlanned)},
                 {EntityMapper.ColName<TreatmentApprovalDal>(x => x.TriggerThreshold)},
                 {EntityMapper.ColName<TreatmentApprovalDal>(x => x.OwnerThreshold)},
                 {EntityMapper.ColName<TreatmentApprovalDal>(x => x.DirectorThreshold)},
                 {EntityMapper.ColName<TreatmentApprovalDal>(x => x.CeoThreshold)},
                 {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RequestedById)},
                 {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RequestedAt)},
                 {EntityMapper.ColName<TreatmentApprovalDal>(x => x.DecidedById)},
                 {EntityMapper.ColName<TreatmentApprovalDal>(x => x.DecidedAt)},
                 {EntityMapper.ColName<TreatmentApprovalDal>(x => x.DecisionReason)},
                 {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RevokedById)},
                 {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RevokedAt)},
                 {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RevokeReason)})
            VALUES
                ({EntityMapper.ParameterName(nameof(TreatmentApprovalDal.Id))},
                 {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.Version))},
                 {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.TreatmentId))},
                 {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.Status))},
                 {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.RequiredLevel))},
                 {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.CostPlanned))},
                 {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.TriggerThreshold))},
                 {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.OwnerThreshold))},
                 {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.DirectorThreshold))},
                 {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.CeoThreshold))},
                 {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.RequestedById))},
                 {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.RequestedAt))},
                 {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.DecidedById))},
                 {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.DecidedAt))},
                 {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.DecisionReason))},
                 {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.RevokedById))},
                 {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.RevokedAt))},
                 {EntityMapper.ParameterName(nameof(TreatmentApprovalDal.RevokeReason))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }

    /// <summary>Добавляет новую запись DemoDataSeedDal в транзакцию первоначального заполнения</summary>
    /// <param name="row">Новая запись</param>
    /// <param name="transaction">Общая транзакция набора</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task InsertAsync(DemoDataSeedDal row, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<DemoDataSeedDal>()}
                ({EntityMapper.ColName<DemoDataSeedDal>(x => x.Id)},
                 {EntityMapper.ColName<DemoDataSeedDal>(x => x.AppliedAt)})
            VALUES
                ({EntityMapper.ParameterName(nameof(DemoDataSeedDal.Id))},
                 {EntityMapper.ParameterName(nameof(DemoDataSeedDal.AppliedAt))})
            """;
        await connection.ExecuteAsync(new CommandDefinition(query, row, transaction, cancellationToken: cancellationToken));
    }
}
