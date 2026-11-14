using Core.Database.DapperExtensions.EntityMapper;
using Domain.DalModels;
using FluentMigrator;

namespace Infrastructure.Migrations;

/// <summary>Добавляет реестр, справочники и надёжную запись бизнес-истории клиента</summary>
[Migration(202611140300)]
public sealed class Date_202611140300_AddRiskRegistry : Migration
{
    /// <summary>Сообщение об отказе от отката с потерей истории</summary>
    private const string RestoreRequired = "Откат реестра рисков выполняется восстановлением резервной копии";

    /// <summary>Индекс ActiveHazardGroup</summary>
    private const string ActiveHazardGroup = "IX_Risk_ActiveHazardGroup";

    /// <summary>Индекс ActiveHazard</summary>
    private const string ActiveHazard = "IX_Risk_ActiveHazard";

    /// <summary>Индекс ActiveCategory</summary>
    private const string ActiveCategory = "IX_Risk_ActiveCategory";

    /// <summary>Индекс LivePrimary</summary>
    private const string LivePrimary = "IX_Risk_LivePrimary";

    /// <summary>Индекс LiveProcess</summary>
    private const string LiveProcess = "IX_Risk_LiveProcess";

    /// <summary>Индекс OwnerChange</summary>
    private const string OwnerChange = "IX_Risk_OwnerChange";

    /// <summary>Индекс CurrentAssessment</summary>
    private const string CurrentAssessment = "IX_Risk_CurrentAssessment";

    /// <summary>Индекс RiskAssessments</summary>
    private const string RiskAssessments = "IX_Risk_RiskAssessments";

    /// <summary>Индекс VisibleComment</summary>
    private const string VisibleComment = "IX_Risk_VisibleComment";

    /// <summary>Индекс EventDate</summary>
    private const string EventDate = "IX_Risk_EventDate";

    /// <summary>Индекс JobDate</summary>
    private const string JobDate = "IX_Risk_JobDate";

    /// <summary>Индекс ExportEvents</summary>
    private const string ExportEvents = "IX_Risk_ExportEvents";

    /// <inheritdoc/>
    public override void Up()
    {
        CreateHazardGroup();
        CreateHazard();
        CreateRiskCategory();
        CreateRisk();
        CreateRiskProcess();
        CreateRiskComment();
        CreateRiskAssessment();
        CreateBusinessChange();
        CreateBusinessChangeJob();
        CreateBusinessExport();
        CreateBusinessExportRow();
    }

    /// <inheritdoc/>
    public override void Down()
    {
        throw new NotSupportedException(RestoreRequired);
    }

    /// <summary>Добавляет таблицу HazardGroup и её индексы</summary>
    private void CreateHazardGroup()
    {
        var table = Schema.Table(EntityMapper.TbName<HazardGroupDal>(false));
        var exists = table.Exists();
        if (!exists)
        {
            var createQuery = $"""
                CREATE TABLE {EntityMapper.TbName<HazardGroupDal>()} (
                    {EntityMapper.ColName<HazardGroupDal>(x => x.Id)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<HazardGroupDal>(x => x.Version)} uuid NOT NULL DEFAULT gen_random_uuid(),
                    {EntityMapper.ColName<HazardGroupDal>(x => x.Name)} text NOT NULL,
                    {EntityMapper.ColName<HazardGroupDal>(x => x.Description)} text,
                    {EntityMapper.ColName<HazardGroupDal>(x => x.IsActive)} boolean NOT NULL DEFAULT true
                )
                """;
            Execute.Sql(createQuery);
        }

        if (!exists || !table.Index(ActiveHazardGroup).Exists())
        {
            var indexQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(ActiveHazardGroup)}
                ON {EntityMapper.TbName<HazardGroupDal>()} (lower({EntityMapper.ColName<HazardGroupDal>(x => x.Name)}))
                WHERE {EntityMapper.ColName<HazardGroupDal>(x => x.IsActive)}
                """;
            Execute.Sql(indexQuery);
        }
    }

    /// <summary>Добавляет таблицу Hazard и её индексы</summary>
    private void CreateHazard()
    {
        var table = Schema.Table(EntityMapper.TbName<HazardDal>(false));
        var exists = table.Exists();
        if (!exists)
        {
            var createQuery = $"""
                CREATE TABLE {EntityMapper.TbName<HazardDal>()} (
                    {EntityMapper.ColName<HazardDal>(x => x.Id)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<HazardDal>(x => x.Version)} uuid NOT NULL DEFAULT gen_random_uuid(),
                    {EntityMapper.ColName<HazardDal>(x => x.Name)} text NOT NULL,
                    {EntityMapper.ColName<HazardDal>(x => x.Description)} text,
                    {EntityMapper.ColName<HazardDal>(x => x.IsActive)} boolean NOT NULL DEFAULT true,
                    {EntityMapper.ColName<HazardDal>(x => x.GroupId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<HazardGroupDal>()} ({EntityMapper.ColName<HazardGroupDal>(x => x.Id)})
                )
                """;
            Execute.Sql(createQuery);
        }

        if (!exists || !table.Index(ActiveHazard).Exists())
        {
            var indexQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(ActiveHazard)}
                ON {EntityMapper.TbName<HazardDal>()} (lower({EntityMapper.ColName<HazardDal>(x => x.Name)}))
                WHERE {EntityMapper.ColName<HazardDal>(x => x.IsActive)}
                """;
            Execute.Sql(indexQuery);
        }
    }

    /// <summary>Добавляет таблицу RiskCategory и её индексы</summary>
    private void CreateRiskCategory()
    {
        var table = Schema.Table(EntityMapper.TbName<RiskCategoryDal>(false));
        var exists = table.Exists();
        if (!exists)
        {
            var createQuery = $"""
                CREATE TABLE {EntityMapper.TbName<RiskCategoryDal>()} (
                    {EntityMapper.ColName<RiskCategoryDal>(x => x.Id)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<RiskCategoryDal>(x => x.Version)} uuid NOT NULL DEFAULT gen_random_uuid(),
                    {EntityMapper.ColName<RiskCategoryDal>(x => x.Name)} text NOT NULL,
                    {EntityMapper.ColName<RiskCategoryDal>(x => x.Description)} text,
                    {EntityMapper.ColName<RiskCategoryDal>(x => x.IsActive)} boolean NOT NULL DEFAULT true
                )
                """;
            Execute.Sql(createQuery);
        }

        if (!exists || !table.Index(ActiveCategory).Exists())
        {
            var indexQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(ActiveCategory)}
                ON {EntityMapper.TbName<RiskCategoryDal>()} (lower({EntityMapper.ColName<RiskCategoryDal>(x => x.Name)}))
                WHERE {EntityMapper.ColName<RiskCategoryDal>(x => x.IsActive)}
                """;
            Execute.Sql(indexQuery);
        }
    }

    /// <summary>Добавляет таблицу Risk и её индексы</summary>
    private void CreateRisk()
    {
        var table = Schema.Table(EntityMapper.TbName<RiskDal>(false));
        var exists = table.Exists();
        if (!exists)
        {
            var createQuery = $"""
                CREATE TABLE {EntityMapper.TbName<RiskDal>()} (
                    {EntityMapper.ColName<RiskDal>(x => x.Id)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<RiskDal>(x => x.Version)} uuid NOT NULL DEFAULT gen_random_uuid(),
                    {EntityMapper.ColName<RiskDal>(x => x.Name)} text NOT NULL,
                    {EntityMapper.ColName<RiskDal>(x => x.Description)} text NOT NULL,
                    {EntityMapper.ColName<RiskDal>(x => x.HazardId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<HazardDal>()} ({EntityMapper.ColName<HazardDal>(x => x.Id)}),
                    {EntityMapper.ColName<RiskDal>(x => x.CategoryId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<RiskCategoryDal>()} ({EntityMapper.ColName<RiskCategoryDal>(x => x.Id)}),
                    {EntityMapper.ColName<RiskDal>(x => x.AuthorId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<UserProfileDal>()} ({EntityMapper.ColName<UserProfileDal>(x => x.UserId)}),
                    {EntityMapper.ColName<RiskDal>(x => x.AssetId)} uuid REFERENCES {EntityMapper.TbName<AssetDal>()} ({EntityMapper.ColName<AssetDal>(x => x.Id)}),
                    {EntityMapper.ColName<RiskDal>(x => x.Status)} text NOT NULL,
                    {EntityMapper.ColName<RiskDal>(x => x.AcceptanceLevel)} text,
                    {EntityMapper.ColName<RiskDal>(x => x.CloseReason)} text,
                    {EntityMapper.ColName<RiskDal>(x => x.CreatedAt)} timestamptz NOT NULL DEFAULT clock_timestamp(),
                    {EntityMapper.ColName<RiskDal>(x => x.UpdatedAt)} timestamptz NOT NULL DEFAULT clock_timestamp()
                )
                """;
            Execute.Sql(createQuery);
        }
    }

    /// <summary>Добавляет таблицу RiskProcess и её индексы</summary>
    private void CreateRiskProcess()
    {
        var table = Schema.Table(EntityMapper.TbName<RiskProcessDal>(false));
        var exists = table.Exists();
        if (!exists)
        {
            var createQuery = $"""
                CREATE TABLE {EntityMapper.TbName<RiskProcessDal>()} (
                    {EntityMapper.ColName<RiskProcessDal>(x => x.Id)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<RiskProcessDal>(x => x.Version)} uuid NOT NULL DEFAULT gen_random_uuid(),
                    {EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<RiskDal>()} ({EntityMapper.ColName<RiskDal>(x => x.Id)}),
                    {EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<ProcessDal>()} ({EntityMapper.ColName<ProcessDal>(x => x.Id)}),
                    {EntityMapper.ColName<RiskProcessDal>(x => x.OwnerId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<OrgPersonDal>()} ({EntityMapper.ColName<OrgPersonDal>(x => x.Id)}),
                    {EntityMapper.ColName<RiskProcessDal>(x => x.IsPrimary)} boolean NOT NULL,
                    {EntityMapper.ColName<RiskProcessDal>(x => x.LinkedAt)} timestamptz NOT NULL DEFAULT clock_timestamp(),
                    {EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} timestamptz
                )
                """;
            Execute.Sql(createQuery);
        }

        if (!exists || !table.Index(LivePrimary).Exists())
        {
            var indexQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(LivePrimary)}
                ON {EntityMapper.TbName<RiskProcessDal>()} ({EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)})
                WHERE {EntityMapper.ColName<RiskProcessDal>(x => x.IsPrimary)} AND {EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL
                """;
            Execute.Sql(indexQuery);
        }

        if (!exists || !table.Index(LiveProcess).Exists())
        {
            var indexQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(LiveProcess)}
                ON {EntityMapper.TbName<RiskProcessDal>()} ({EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)}, {EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)})
                WHERE {EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL
                """;
            Execute.Sql(indexQuery);
        }

        if (!exists || !table.Index(OwnerChange).Exists())
        {
            var indexQuery = $"""
                CREATE INDEX {EntityMapper.Identifier(OwnerChange)}
                ON {EntityMapper.TbName<RiskProcessDal>()} ({EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)}, {EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)})
                WHERE {EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL
                """;
            Execute.Sql(indexQuery);
        }
    }

    /// <summary>Добавляет таблицу RiskComment и её индексы</summary>
    private void CreateRiskComment()
    {
        var table = Schema.Table(EntityMapper.TbName<RiskCommentDal>(false));
        var exists = table.Exists();
        if (!exists)
        {
            var createQuery = $"""
                CREATE TABLE {EntityMapper.TbName<RiskCommentDal>()} (
                    {EntityMapper.ColName<RiskCommentDal>(x => x.Id)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<RiskCommentDal>(x => x.Version)} uuid NOT NULL DEFAULT gen_random_uuid(),
                    {EntityMapper.ColName<RiskCommentDal>(x => x.RiskId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<RiskDal>()} ({EntityMapper.ColName<RiskDal>(x => x.Id)}),
                    {EntityMapper.ColName<RiskCommentDal>(x => x.AuthorId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<UserProfileDal>()} ({EntityMapper.ColName<UserProfileDal>(x => x.UserId)}),
                    {EntityMapper.ColName<RiskCommentDal>(x => x.Text)} text NOT NULL,
                    {EntityMapper.ColName<RiskCommentDal>(x => x.IsSystem)} boolean NOT NULL,
                    {EntityMapper.ColName<RiskCommentDal>(x => x.IsDeleted)} boolean NOT NULL,
                    {EntityMapper.ColName<RiskCommentDal>(x => x.DeletedAt)} timestamptz,
                    {EntityMapper.ColName<RiskCommentDal>(x => x.DeletedById)} uuid REFERENCES {EntityMapper.TbName<UserProfileDal>()} ({EntityMapper.ColName<UserProfileDal>(x => x.UserId)}),
                    {EntityMapper.ColName<RiskCommentDal>(x => x.CreatedAt)} timestamptz NOT NULL DEFAULT clock_timestamp(),
                    {EntityMapper.ColName<RiskCommentDal>(x => x.UpdatedAt)} timestamptz NOT NULL DEFAULT clock_timestamp()
                )
                """;
            Execute.Sql(createQuery);
        }

        if (!exists || !table.Index(VisibleComment).Exists())
        {
            var indexQuery = $"""
                CREATE INDEX {EntityMapper.Identifier(VisibleComment)}
                ON {EntityMapper.TbName<RiskCommentDal>()} ({EntityMapper.ColName<RiskCommentDal>(x => x.RiskId)}, {EntityMapper.ColName<RiskCommentDal>(x => x.CreatedAt)}, {EntityMapper.ColName<RiskCommentDal>(x => x.Id)})
                WHERE NOT {EntityMapper.ColName<RiskCommentDal>(x => x.IsDeleted)}
                """;
            Execute.Sql(indexQuery);
        }
    }

    /// <summary>Добавляет таблицу RiskAssessment и её индексы</summary>
    private void CreateRiskAssessment()
    {
        var table = Schema.Table(EntityMapper.TbName<RiskAssessmentDal>(false));
        var exists = table.Exists();
        if (!exists)
        {
            var createQuery = $"""
                CREATE TABLE {EntityMapper.TbName<RiskAssessmentDal>()} (
                    {EntityMapper.ColName<RiskAssessmentDal>(x => x.Id)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<RiskAssessmentDal>(x => x.Version)} uuid NOT NULL DEFAULT gen_random_uuid(),
                    {EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<RiskDal>()} ({EntityMapper.ColName<RiskDal>(x => x.Id)}),
                    {EntityMapper.ColName<RiskAssessmentDal>(x => x.LikelihoodLevelId)} uuid NOT NULL,
                    {EntityMapper.ColName<RiskAssessmentDal>(x => x.ImpactLevelId)} uuid NOT NULL,
                    {EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskScore)} numeric NOT NULL,
                    {EntityMapper.ColName<RiskAssessmentDal>(x => x.IsActive)} boolean NOT NULL DEFAULT true,
                    {EntityMapper.ColName<RiskAssessmentDal>(x => x.CreatedAt)} timestamptz NOT NULL DEFAULT clock_timestamp(),
                    {EntityMapper.ColName<RiskAssessmentDal>(x => x.ResponseMode)} text
                )
                """;
            Execute.Sql(createQuery);
        }

        if (!exists || !table.Index(CurrentAssessment).Exists())
        {
            var indexQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(CurrentAssessment)}
                ON {EntityMapper.TbName<RiskAssessmentDal>()} ({EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskId)})
                WHERE {EntityMapper.ColName<RiskAssessmentDal>(x => x.IsActive)}
                """;
            Execute.Sql(indexQuery);
        }

        if (!exists || !table.Index(RiskAssessments).Exists())
        {
            var indexQuery = $"""
                CREATE INDEX {EntityMapper.Identifier(RiskAssessments)}
                ON {EntityMapper.TbName<RiskAssessmentDal>()} ({EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskId)})
                """;
            Execute.Sql(indexQuery);
        }
    }

    /// <summary>Добавляет таблицу BusinessChange и её индексы</summary>
    private void CreateBusinessChange()
    {
        var table = Schema.Table(EntityMapper.TbName<BusinessChangeDal>(false));
        var exists = table.Exists();
        if (!exists)
        {
            var createQuery = $"""
                CREATE TABLE {EntityMapper.TbName<BusinessChangeDal>()} (
                    {EntityMapper.ColName<BusinessChangeDal>(x => x.EventId)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)} timestamptz NOT NULL,
                    {EntityMapper.ColName<BusinessChangeDal>(x => x.Payload)} jsonb NOT NULL
                )
                """;
            Execute.Sql(createQuery);
        }

        if (!exists || !table.Index(EventDate).Exists())
        {
            var indexQuery = $"""
                CREATE INDEX {EntityMapper.Identifier(EventDate)}
                ON {EntityMapper.TbName<BusinessChangeDal>()} ({EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)}, {EntityMapper.ColName<BusinessChangeDal>(x => x.EventId)})
                """;
            Execute.Sql(indexQuery);
        }
    }

    /// <summary>Добавляет таблицу BusinessChangeJob и её индексы</summary>
    private void CreateBusinessChangeJob()
    {
        var table = Schema.Table(EntityMapper.TbName<BusinessChangeJobDal>(false));
        var exists = table.Exists();
        if (!exists)
        {
            var createQuery = $"""
                CREATE TABLE {EntityMapper.TbName<BusinessChangeJobDal>()} (
                    {EntityMapper.ColName<BusinessChangeJobDal>(x => x.EventId)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<BusinessChangeJobDal>(x => x.OccurredAt)} timestamptz NOT NULL,
                    {EntityMapper.ColName<BusinessChangeJobDal>(x => x.Payload)} jsonb NOT NULL
                )
                """;
            Execute.Sql(createQuery);
        }

        if (!exists || !table.Index(JobDate).Exists())
        {
            var indexQuery = $"""
                CREATE INDEX {EntityMapper.Identifier(JobDate)}
                ON {EntityMapper.TbName<BusinessChangeJobDal>()} ({EntityMapper.ColName<BusinessChangeJobDal>(x => x.OccurredAt)}, {EntityMapper.ColName<BusinessChangeJobDal>(x => x.EventId)})
                """;
            Execute.Sql(indexQuery);
        }
    }

    /// <summary>Добавляет таблицу BusinessExport и её индексы</summary>
    private void CreateBusinessExport()
    {
        var table = Schema.Table(EntityMapper.TbName<BusinessExportDal>(false));
        var exists = table.Exists();
        if (!exists)
        {
            var createQuery = $"""
                CREATE TABLE {EntityMapper.TbName<BusinessExportDal>()} (
                    {EntityMapper.ColName<BusinessExportDal>(x => x.Id)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<BusinessExportDal>(x => x.From)} date NOT NULL,
                    {EntityMapper.ColName<BusinessExportDal>(x => x.To)} date NOT NULL,
                    {EntityMapper.ColName<BusinessExportDal>(x => x.ActorId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<UserProfileDal>()} ({EntityMapper.ColName<UserProfileDal>(x => x.UserId)}),
                    {EntityMapper.ColName<BusinessExportDal>(x => x.CreatedAt)} timestamptz NOT NULL DEFAULT clock_timestamp()
                )
                """;
            Execute.Sql(createQuery);
        }
    }

    /// <summary>Добавляет таблицу BusinessExportRow и её индексы</summary>
    private void CreateBusinessExportRow()
    {
        var table = Schema.Table(EntityMapper.TbName<BusinessExportRowDal>(false));
        var exists = table.Exists();
        if (!exists)
        {
            var createQuery = $"""
                CREATE TABLE {EntityMapper.TbName<BusinessExportRowDal>()} (
                    {EntityMapper.ColName<BusinessExportRowDal>(x => x.ExportId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<BusinessExportDal>()} ({EntityMapper.ColName<BusinessExportDal>(x => x.Id)}),
                    {EntityMapper.ColName<BusinessExportRowDal>(x => x.EventId)} uuid NOT NULL,
                    {EntityMapper.ColName<BusinessExportRowDal>(x => x.Payload)} jsonb NOT NULL
                )
                """;
            Execute.Sql(createQuery);
        }

        if (!exists || !table.Index(ExportEvents).Exists())
        {
            var indexQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(ExportEvents)}
                ON {EntityMapper.TbName<BusinessExportRowDal>()} ({EntityMapper.ColName<BusinessExportRowDal>(x => x.ExportId)}, {EntityMapper.ColName<BusinessExportRowDal>(x => x.EventId)})
                """;
            Execute.Sql(indexQuery);
        }
    }
}
