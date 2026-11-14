using Application.Constants;
using Core.Database.DapperExtensions.EntityMapper;
using Domain.DalModels;
using FluentMigrator;

namespace Infrastructure.Migrations;

/// <summary>Добавляет меры, согласования, исполнителей и комментарии</summary>
[Migration(202611140500)]
public sealed class Date_202611140500_AddTreatments : Migration
{
    /// <summary>Индекс слота пары риск–процесс</summary>
    internal const string SlotIndex = "UX_Treatment_RiskProcessSlot";

    /// <summary>Индекс открытого запроса</summary>
    private const string PendingApprovalIndex = "UX_Treatment_PendingApproval";

    /// <summary>Индекс текущего назначения</summary>
    private const string CurrentAssignmentIndex = "UX_Treatment_CurrentAssignment";

    /// <summary>Ключ настройки порогов клиента</summary>
    internal const string SettingsId = "03c469b1-93d5-4fb6-b226-2a8fea4ff536";

    /// <summary>Исходный порог согласования владельцем</summary>
    private const int DefaultOwnerThreshold = 0;

    /// <summary>Исходный порог согласования директором</summary>
    private const int DefaultDirectorThreshold = 50_000;

    /// <summary>Исходный порог согласования CEO</summary>
    private const int DefaultCeoThreshold = 100_000;

    /// <summary>Уникальный ключ для проверки стабильной пары через связь риска</summary>
    private const string RiskProcessKey = "UX_Treatment_RiskProcessLink";

    /// <summary>Сообщение отката миграции</summary>
    private const string RestoreRequired = "Откат схемы мер выполняется восстановлением резервной копии";

    /// <inheritdoc/>
    public override void Up()
    {
        var linkTable = Schema.Table(EntityMapper.TbName<RiskProcessDal>(false));
        if (linkTable.Exists() && !linkTable.Index(RiskProcessKey).Exists())
        {
            var keyQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(RiskProcessKey)} ON {EntityMapper.TbName<RiskProcessDal>()}
                    ({EntityMapper.ColName<RiskProcessDal>(x => x.Id)}, {EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)}, {EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)})
                """;
            Execute.Sql(keyQuery);
        }

        var treatment = Schema.Table(EntityMapper.TbName<TreatmentDal>(false));
        var treatmentExists = treatment.Exists();
        if (!treatmentExists)
        {
            var createQuery = $"""
                CREATE TABLE {EntityMapper.TbName<TreatmentDal>()} (
                    {EntityMapper.ColName<TreatmentDal>(x => x.Id)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<TreatmentDal>(x => x.Version)} uuid NOT NULL DEFAULT gen_random_uuid(),
                    {EntityMapper.ColName<TreatmentDal>(x => x.RiskId)} uuid NOT NULL,
                    {EntityMapper.ColName<TreatmentDal>(x => x.ProcessId)} uuid NOT NULL,
                    {EntityMapper.ColName<TreatmentDal>(x => x.RiskProcessId)} uuid NOT NULL,
                    {EntityMapper.ColName<TreatmentDal>(x => x.Title)} text NOT NULL,
                    {EntityMapper.ColName<TreatmentDal>(x => x.Description)} text,
                    {EntityMapper.ColName<TreatmentDal>(x => x.DueDate)} timestamp NOT NULL,
                    {EntityMapper.ColName<TreatmentDal>(x => x.CostPlanned)} numeric NOT NULL,
                    {EntityMapper.ColName<TreatmentDal>(x => x.CostActual)} numeric,
                    {EntityMapper.ColName<TreatmentDal>(x => x.Status)} text NOT NULL,
                    {EntityMapper.ColName<TreatmentDal>(x => x.CreatedById)} uuid NOT NULL REFERENCES {EntityMapper.TbName<UserProfileDal>()} ({EntityMapper.ColName<UserProfileDal>(x => x.UserId)}),
                    {EntityMapper.ColName<TreatmentDal>(x => x.CreatedAt)} timestamptz NOT NULL DEFAULT clock_timestamp(),
                    {EntityMapper.ColName<TreatmentDal>(x => x.UpdatedAt)} timestamptz NOT NULL DEFAULT clock_timestamp(),
                    FOREIGN KEY ({EntityMapper.ColName<TreatmentDal>(x => x.RiskProcessId)}, {EntityMapper.ColName<TreatmentDal>(x => x.RiskId)}, {EntityMapper.ColName<TreatmentDal>(x => x.ProcessId)})
                        REFERENCES {EntityMapper.TbName<RiskProcessDal>()} ({EntityMapper.ColName<RiskProcessDal>(x => x.Id)}, {EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)}, {EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)})
                )
                """;
            Execute.Sql(createQuery);
        }

        if (!treatmentExists || !treatment.Index(SlotIndex).Exists())
        {
            var indexQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(SlotIndex)} ON {EntityMapper.TbName<TreatmentDal>()} ({EntityMapper.ColName<TreatmentDal>(x => x.RiskId)}, {EntityMapper.ColName<TreatmentDal>(x => x.ProcessId)})
                WHERE {EntityMapper.ColName<TreatmentDal>(x => x.Status)} IN ('{TreatmentRules.Pending}', '{TreatmentRules.Approved}', '{TreatmentRules.Waiting}', '{TreatmentRules.Working}')
                """;
            Execute.Sql(indexQuery);
        }

        var treatmentAssignment = Schema.Table(EntityMapper.TbName<TreatmentAssignmentDal>(false));
        var treatmentAssignmentExists = treatmentAssignment.Exists();
        if (!treatmentAssignmentExists)
        {
            var createQuery = $"""
                CREATE TABLE {EntityMapper.TbName<TreatmentAssignmentDal>()} (
                    {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.Id)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.TreatmentId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<TreatmentDal>()} ({EntityMapper.ColName<TreatmentDal>(x => x.Id)}),
                    {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssigneeId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<OrgPersonDal>()} ({EntityMapper.ColName<OrgPersonDal>(x => x.Id)}),
                    {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignedById)} uuid NOT NULL REFERENCES {EntityMapper.TbName<UserProfileDal>()} ({EntityMapper.ColName<UserProfileDal>(x => x.UserId)}),
                    {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignedAt)} timestamptz NOT NULL DEFAULT clock_timestamp(),
                    {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignmentSource)} text NOT NULL
                )
                """;
            Execute.Sql(createQuery);
        }

        if (!treatmentAssignmentExists || !treatmentAssignment.Index(CurrentAssignmentIndex).Exists())
        {
            var indexQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(CurrentAssignmentIndex)} ON {EntityMapper.TbName<TreatmentAssignmentDal>()} ({EntityMapper.ColName<TreatmentAssignmentDal>(x => x.TreatmentId)})
                """;
            Execute.Sql(indexQuery);
        }

        var treatmentApproval = Schema.Table(EntityMapper.TbName<TreatmentApprovalDal>(false));
        var treatmentApprovalExists = treatmentApproval.Exists();
        if (!treatmentApprovalExists)
        {
            var createQuery = $"""
                CREATE TABLE {EntityMapper.TbName<TreatmentApprovalDal>()} (
                    {EntityMapper.ColName<TreatmentApprovalDal>(x => x.Id)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<TreatmentApprovalDal>(x => x.Version)} uuid NOT NULL DEFAULT gen_random_uuid(),
                    {EntityMapper.ColName<TreatmentApprovalDal>(x => x.TreatmentId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<TreatmentDal>()} ({EntityMapper.ColName<TreatmentDal>(x => x.Id)}),
                    {EntityMapper.ColName<TreatmentApprovalDal>(x => x.Status)} text NOT NULL,
                    {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RequiredLevel)} text NOT NULL,
                    {EntityMapper.ColName<TreatmentApprovalDal>(x => x.CostPlanned)} numeric NOT NULL,
                    {EntityMapper.ColName<TreatmentApprovalDal>(x => x.TriggerThreshold)} numeric NOT NULL,
                    {EntityMapper.ColName<TreatmentApprovalDal>(x => x.OwnerThreshold)} numeric NOT NULL,
                    {EntityMapper.ColName<TreatmentApprovalDal>(x => x.DirectorThreshold)} numeric NOT NULL,
                    {EntityMapper.ColName<TreatmentApprovalDal>(x => x.CeoThreshold)} numeric NOT NULL,
                    {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RequestedById)} uuid NOT NULL REFERENCES {EntityMapper.TbName<UserProfileDal>()} ({EntityMapper.ColName<UserProfileDal>(x => x.UserId)}),
                    {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RequestedAt)} timestamptz NOT NULL DEFAULT clock_timestamp(),
                    {EntityMapper.ColName<TreatmentApprovalDal>(x => x.DecidedById)} uuid REFERENCES {EntityMapper.TbName<UserProfileDal>()} ({EntityMapper.ColName<UserProfileDal>(x => x.UserId)}),
                    {EntityMapper.ColName<TreatmentApprovalDal>(x => x.DecidedAt)} timestamptz,
                    {EntityMapper.ColName<TreatmentApprovalDal>(x => x.DecisionReason)} text,
                    {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RevokedById)} uuid REFERENCES {EntityMapper.TbName<UserProfileDal>()} ({EntityMapper.ColName<UserProfileDal>(x => x.UserId)}),
                    {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RevokedAt)} timestamptz,
                    {EntityMapper.ColName<TreatmentApprovalDal>(x => x.RevokeReason)} text
                )
                """;
            Execute.Sql(createQuery);
        }

        if (!treatmentApprovalExists || !treatmentApproval.Index(PendingApprovalIndex).Exists())
        {
            var indexQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(PendingApprovalIndex)} ON {EntityMapper.TbName<TreatmentApprovalDal>()} ({EntityMapper.ColName<TreatmentApprovalDal>(x => x.TreatmentId)})
                WHERE {EntityMapper.ColName<TreatmentApprovalDal>(x => x.Status)} = '{TreatmentRules.Pending}'
                """;
            Execute.Sql(indexQuery);
        }

        var treatmentApprovalSettings = Schema.Table(EntityMapper.TbName<TreatmentApprovalSettingsDal>(false));
        var treatmentApprovalSettingsExists = treatmentApprovalSettings.Exists();
        if (!treatmentApprovalSettingsExists)
        {
            var createQuery = $"""
                CREATE TABLE {EntityMapper.TbName<TreatmentApprovalSettingsDal>()} (
                    {EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.Id)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.Version)} uuid NOT NULL DEFAULT gen_random_uuid(),
                    {EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.OwnerThreshold)} numeric NOT NULL,
                    {EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.DirectorThreshold)} numeric NOT NULL,
                    {EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.CeoThreshold)} numeric NOT NULL
                )
                """;
            Execute.Sql(createQuery);
        }

        var treatmentComment = Schema.Table(EntityMapper.TbName<TreatmentCommentDal>(false));
        var treatmentCommentExists = treatmentComment.Exists();
        if (!treatmentCommentExists)
        {
            var createQuery = $"""
                CREATE TABLE {EntityMapper.TbName<TreatmentCommentDal>()} (
                    {EntityMapper.ColName<TreatmentCommentDal>(x => x.Id)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<TreatmentCommentDal>(x => x.Version)} uuid NOT NULL DEFAULT gen_random_uuid(),
                    {EntityMapper.ColName<TreatmentCommentDal>(x => x.TreatmentId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<TreatmentDal>()} ({EntityMapper.ColName<TreatmentDal>(x => x.Id)}),
                    {EntityMapper.ColName<TreatmentCommentDal>(x => x.AuthorId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<UserProfileDal>()} ({EntityMapper.ColName<UserProfileDal>(x => x.UserId)}),
                    {EntityMapper.ColName<TreatmentCommentDal>(x => x.Text)} text NOT NULL,
                    {EntityMapper.ColName<TreatmentCommentDal>(x => x.CreatedAt)} timestamptz NOT NULL DEFAULT clock_timestamp(),
                    {EntityMapper.ColName<TreatmentCommentDal>(x => x.UpdatedAt)} timestamptz NOT NULL DEFAULT clock_timestamp(),
                    {EntityMapper.ColName<TreatmentCommentDal>(x => x.IsDeleted)} boolean NOT NULL DEFAULT false,
                    {EntityMapper.ColName<TreatmentCommentDal>(x => x.DeletedAt)} timestamptz,
                    {EntityMapper.ColName<TreatmentCommentDal>(x => x.DeletedById)} uuid REFERENCES {EntityMapper.TbName<UserProfileDal>()} ({EntityMapper.ColName<UserProfileDal>(x => x.UserId)})
                )
                """;
            Execute.Sql(createQuery);
        }

        var seedQuery = $"""
            INSERT INTO {EntityMapper.TbName<TreatmentApprovalSettingsDal>()}
                ({EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.Id)}, {EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.OwnerThreshold)},
                 {EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.DirectorThreshold)}, {EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.CeoThreshold)})
            VALUES ('{SettingsId}', {DefaultOwnerThreshold}, {DefaultDirectorThreshold}, {DefaultCeoThreshold})
            ON CONFLICT ({EntityMapper.ColName<TreatmentApprovalSettingsDal>(x => x.Id)}) DO NOTHING
            """;
        Execute.Sql(seedQuery);
    }

    /// <inheritdoc/>
    public override void Down()
    {
        throw new NotSupportedException(RestoreRequired);
    }
}
