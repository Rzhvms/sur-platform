using Application.Constants;
using Core.Database.DapperExtensions.EntityMapper;
using Domain.DalModels;
using FluentMigrator;

namespace Infrastructure.Migrations;

/// <summary>
/// Добавляет справочники, организационные назначения и снимки истории в базе клиента
/// </summary>
[Migration(202611140200)]
public sealed class Date_202611140200_AddOrganization : Migration
{
    /// <summary>
    /// Причина отказа от отката с потерей истории
    /// </summary>
    private const string RestoreRequired = "Откат оргструктуры выполняется восстановлением резервной копии";

    /// <summary>
    /// Имя индекса DirectorateName
    /// </summary>
    private const string DirectorateName = "IX_Org_DirectorateName";

    /// <summary>
    /// Имя индекса ActivePerson
    /// </summary>
    private const string ActivePerson = "IX_Org_ActivePerson";

    /// <summary>
    /// Имя индекса ProcessName
    /// </summary>
    private const string ProcessName = "IX_Org_ProcessName";

    /// <summary>
    /// Имя индекса AssetName
    /// </summary>
    private const string AssetName = "IX_Org_AssetName";

    /// <summary>
    /// Имя индекса ActiveCeo
    /// </summary>
    private const string ActiveCeo = "IX_Org_ActiveCeo";

    /// <summary>
    /// Имя индекса ActiveDirectorateResponsibility
    /// </summary>
    private const string ActiveDirectorateResponsibility = "IX_Org_ActiveDirectorateResponsibility";

    /// <summary>
    /// Имя индекса ActiveProcessOwner
    /// </summary>
    private const string ActiveProcessOwner = "IX_Org_ActiveProcessOwner";

    /// <summary>
    /// Имя индекса ActivePersonResponsibility
    /// </summary>
    private const string ActivePersonResponsibility = "IX_Org_ActivePersonResponsibility";

    /// <summary>
    /// Имя индекса HistoryEntity
    /// </summary>
    private const string HistoryEntity = "IX_Org_HistoryEntity";

    /// <inheritdoc/>
    public override void Up()
    {
        CreateDirectorate();
        CreateOrgPerson();
        CreateProcess();
        CreateAsset();
        CreateResponsibility();
        CreateOrgHistory();
    }

    /// <inheritdoc/>
    public override void Down()
    {
        throw new NotSupportedException(RestoreRequired);
    }

    /// <summary>
    /// Создаёт таблицу Directorate и её индексы
    /// </summary>
    private void CreateDirectorate()
    {
        var table = Schema.Table(EntityMapper.TbName<DirectorateDal>(false));
        var exists = table.Exists();
        if (!exists)
        {
            var createTableQuery = $"""
                CREATE TABLE {EntityMapper.TbName<DirectorateDal>()} (
                    {EntityMapper.ColName<DirectorateDal>(x => x.Id)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<DirectorateDal>(x => x.Version)} uuid NOT NULL DEFAULT gen_random_uuid(),
                    {EntityMapper.ColName<DirectorateDal>(x => x.IsActive)} boolean NOT NULL DEFAULT true,
                    {EntityMapper.ColName<DirectorateDal>(x => x.CreatedAt)} timestamptz NOT NULL DEFAULT clock_timestamp(),
                    {EntityMapper.ColName<DirectorateDal>(x => x.UpdatedAt)} timestamptz NOT NULL DEFAULT clock_timestamp(),
                    {EntityMapper.ColName<DirectorateDal>(x => x.Name)} text NOT NULL
                )
                """;
            Execute.Sql(createTableQuery);
        }

        if (!exists || !table.Index(DirectorateName).Exists())
        {
            var createDirectorateNameQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(DirectorateName)} ON {EntityMapper.TbName<DirectorateDal>()} (lower({EntityMapper.ColName<DirectorateDal>(x => x.Name)}))
                """;
            Execute.Sql(createDirectorateNameQuery);
        }
    }

    /// <summary>
    /// Создаёт таблицу OrgPerson и её индексы
    /// </summary>
    private void CreateOrgPerson()
    {
        var table = Schema.Table(EntityMapper.TbName<OrgPersonDal>(false));
        var exists = table.Exists();
        if (!exists)
        {
            var createTableQuery = $"""
                CREATE TABLE {EntityMapper.TbName<OrgPersonDal>()} (
                    {EntityMapper.ColName<OrgPersonDal>(x => x.Id)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<OrgPersonDal>(x => x.Version)} uuid NOT NULL DEFAULT gen_random_uuid(),
                    {EntityMapper.ColName<OrgPersonDal>(x => x.IsActive)} boolean NOT NULL DEFAULT true,
                    {EntityMapper.ColName<OrgPersonDal>(x => x.CreatedAt)} timestamptz NOT NULL DEFAULT clock_timestamp(),
                    {EntityMapper.ColName<OrgPersonDal>(x => x.UpdatedAt)} timestamptz NOT NULL DEFAULT clock_timestamp(),
                    {EntityMapper.ColName<OrgPersonDal>(x => x.UserId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<UserProfileDal>()} ({EntityMapper.ColName<UserProfileDal>(x => x.UserId)}),
                    {EntityMapper.ColName<OrgPersonDal>(x => x.DirectorateId)} uuid REFERENCES {EntityMapper.TbName<DirectorateDal>()} ({EntityMapper.ColName<DirectorateDal>(x => x.Id)}),
                    {EntityMapper.ColName<OrgPersonDal>(x => x.PositionName)} text
                )
                """;
            Execute.Sql(createTableQuery);
        }

        if (!exists || !table.Index(ActivePerson).Exists())
        {
            var createActivePersonQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(ActivePerson)} ON {EntityMapper.TbName<OrgPersonDal>()} ({EntityMapper.ColName<OrgPersonDal>(x => x.UserId)})
                WHERE {EntityMapper.ColName<OrgPersonDal>(x => x.IsActive)}
                """;
            Execute.Sql(createActivePersonQuery);
        }
    }

    /// <summary>
    /// Создаёт таблицу Process и её индексы
    /// </summary>
    private void CreateProcess()
    {
        var table = Schema.Table(EntityMapper.TbName<ProcessDal>(false));
        var exists = table.Exists();
        if (!exists)
        {
            var createTableQuery = $"""
                CREATE TABLE {EntityMapper.TbName<ProcessDal>()} (
                    {EntityMapper.ColName<ProcessDal>(x => x.Id)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<ProcessDal>(x => x.Version)} uuid NOT NULL DEFAULT gen_random_uuid(),
                    {EntityMapper.ColName<ProcessDal>(x => x.IsActive)} boolean NOT NULL DEFAULT true,
                    {EntityMapper.ColName<ProcessDal>(x => x.CreatedAt)} timestamptz NOT NULL DEFAULT clock_timestamp(),
                    {EntityMapper.ColName<ProcessDal>(x => x.UpdatedAt)} timestamptz NOT NULL DEFAULT clock_timestamp(),
                    {EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<DirectorateDal>()} ({EntityMapper.ColName<DirectorateDal>(x => x.Id)}),
                    {EntityMapper.ColName<ProcessDal>(x => x.OwnerId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<OrgPersonDal>()} ({EntityMapper.ColName<OrgPersonDal>(x => x.Id)}),
                    {EntityMapper.ColName<ProcessDal>(x => x.Name)} text NOT NULL,
                    {EntityMapper.ColName<ProcessDal>(x => x.Description)} text
                )
                """;
            Execute.Sql(createTableQuery);
        }

        if (!exists || !table.Index(ProcessName).Exists())
        {
            var createProcessNameQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(ProcessName)} ON {EntityMapper.TbName<ProcessDal>()} ({EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)}, lower({EntityMapper.ColName<ProcessDal>(x => x.Name)}))
                """;
            Execute.Sql(createProcessNameQuery);
        }

        if (!exists || !table.Index(ActiveProcessOwner).Exists())
        {
            var createActiveProcessOwnerQuery = $"""
                CREATE INDEX {EntityMapper.Identifier(ActiveProcessOwner)} ON {EntityMapper.TbName<ProcessDal>()} ({EntityMapper.ColName<ProcessDal>(x => x.OwnerId)})
                WHERE {EntityMapper.ColName<ProcessDal>(x => x.IsActive)}
                """;
            Execute.Sql(createActiveProcessOwnerQuery);
        }
    }

    /// <summary>
    /// Создаёт таблицу Asset и её индексы
    /// </summary>
    private void CreateAsset()
    {
        var table = Schema.Table(EntityMapper.TbName<AssetDal>(false));
        var exists = table.Exists();
        if (!exists)
        {
            var createTableQuery = $"""
                CREATE TABLE {EntityMapper.TbName<AssetDal>()} (
                    {EntityMapper.ColName<AssetDal>(x => x.Id)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<AssetDal>(x => x.Version)} uuid NOT NULL DEFAULT gen_random_uuid(),
                    {EntityMapper.ColName<AssetDal>(x => x.IsActive)} boolean NOT NULL DEFAULT true,
                    {EntityMapper.ColName<AssetDal>(x => x.CreatedAt)} timestamptz NOT NULL DEFAULT clock_timestamp(),
                    {EntityMapper.ColName<AssetDal>(x => x.UpdatedAt)} timestamptz NOT NULL DEFAULT clock_timestamp(),
                    {EntityMapper.ColName<AssetDal>(x => x.Name)} text NOT NULL,
                    {EntityMapper.ColName<AssetDal>(x => x.Description)} text,
                    {EntityMapper.ColName<AssetDal>(x => x.InventoryNumber)} text,
                    {EntityMapper.ColName<AssetDal>(x => x.HourlyDowntimeCost)} numeric NOT NULL,
                    CHECK ({EntityMapper.ColName<AssetDal>(x => x.HourlyDowntimeCost)} >= 0 AND {EntityMapper.ColName<AssetDal>(x => x.HourlyDowntimeCost)} = round({EntityMapper.ColName<AssetDal>(x => x.HourlyDowntimeCost)}, 2))
                )
                """;
            Execute.Sql(createTableQuery);
        }

        if (!exists || !table.Index(AssetName).Exists())
        {
            var createAssetNameQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(AssetName)} ON {EntityMapper.TbName<AssetDal>()} (lower({EntityMapper.ColName<AssetDal>(x => x.Name)}))
                """;
            Execute.Sql(createAssetNameQuery);
        }
    }

    /// <summary>
    /// Создаёт таблицу Responsibility и её индексы
    /// </summary>
    private void CreateResponsibility()
    {
        var table = Schema.Table(EntityMapper.TbName<ResponsibilityDal>(false));
        var exists = table.Exists();
        if (!exists)
        {
            var createTableQuery = $"""
                CREATE TABLE {EntityMapper.TbName<ResponsibilityDal>()} (
                    {EntityMapper.ColName<ResponsibilityDal>(x => x.Id)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<ResponsibilityDal>(x => x.Version)} uuid NOT NULL DEFAULT gen_random_uuid(),
                    {EntityMapper.ColName<ResponsibilityDal>(x => x.IsActive)} boolean NOT NULL DEFAULT true,
                    {EntityMapper.ColName<ResponsibilityDal>(x => x.CreatedAt)} timestamptz NOT NULL DEFAULT clock_timestamp(),
                    {EntityMapper.ColName<ResponsibilityDal>(x => x.UpdatedAt)} timestamptz NOT NULL DEFAULT clock_timestamp(),
                    {EntityMapper.ColName<ResponsibilityDal>(x => x.PersonId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<OrgPersonDal>()} ({EntityMapper.ColName<OrgPersonDal>(x => x.Id)}),
                    {EntityMapper.ColName<ResponsibilityDal>(x => x.DirectorateId)} uuid REFERENCES {EntityMapper.TbName<DirectorateDal>()} ({EntityMapper.ColName<DirectorateDal>(x => x.Id)}),
                    {EntityMapper.ColName<ResponsibilityDal>(x => x.ResponsibilityType)} text NOT NULL,
                    CHECK (({EntityMapper.ColName<ResponsibilityDal>(x => x.ResponsibilityType)} = '{OrganizationRoles.Ceo}' AND {EntityMapper.ColName<ResponsibilityDal>(x => x.DirectorateId)} IS NULL) OR ({EntityMapper.ColName<ResponsibilityDal>(x => x.ResponsibilityType)} IN ('{OrganizationRoles.Director}', '{OrganizationRoles.RiskSpecialist}') AND {EntityMapper.ColName<ResponsibilityDal>(x => x.DirectorateId)} IS NOT NULL))
                )
                """;
            Execute.Sql(createTableQuery);
        }

        if (!exists || !table.Index(ActiveCeo).Exists())
        {
            var createActiveCeoQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(ActiveCeo)} ON {EntityMapper.TbName<ResponsibilityDal>()} ({EntityMapper.ColName<ResponsibilityDal>(x => x.ResponsibilityType)})
                WHERE {EntityMapper.ColName<ResponsibilityDal>(x => x.IsActive)} AND {EntityMapper.ColName<ResponsibilityDal>(x => x.DirectorateId)} IS NULL
                """;
            Execute.Sql(createActiveCeoQuery);
        }

        if (!exists || !table.Index(ActiveDirectorateResponsibility).Exists())
        {
            var createActiveDirectorateResponsibilityQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(ActiveDirectorateResponsibility)} ON {EntityMapper.TbName<ResponsibilityDal>()} ({EntityMapper.ColName<ResponsibilityDal>(x => x.DirectorateId)}, {EntityMapper.ColName<ResponsibilityDal>(x => x.ResponsibilityType)})
                WHERE {EntityMapper.ColName<ResponsibilityDal>(x => x.IsActive)} AND {EntityMapper.ColName<ResponsibilityDal>(x => x.DirectorateId)} IS NOT NULL
                """;
            Execute.Sql(createActiveDirectorateResponsibilityQuery);
        }

        if (!exists || !table.Index(ActivePersonResponsibility).Exists())
        {
            var createActivePersonResponsibilityQuery = $"""
                CREATE INDEX {EntityMapper.Identifier(ActivePersonResponsibility)} ON {EntityMapper.TbName<ResponsibilityDal>()} ({EntityMapper.ColName<ResponsibilityDal>(x => x.PersonId)})
                WHERE {EntityMapper.ColName<ResponsibilityDal>(x => x.IsActive)}
                """;
            Execute.Sql(createActivePersonResponsibilityQuery);
        }
    }

    /// <summary>
    /// Создаёт таблицу OrgHistory и её индексы
    /// </summary>
    private void CreateOrgHistory()
    {
        var table = Schema.Table(EntityMapper.TbName<OrgHistoryDal>(false));
        var exists = table.Exists();
        if (!exists)
        {
            var createTableQuery = $"""
                CREATE TABLE {EntityMapper.TbName<OrgHistoryDal>()} (
                    {EntityMapper.ColName<OrgHistoryDal>(x => x.Id)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<OrgHistoryDal>(x => x.EntityId)} uuid NOT NULL,
                    {EntityMapper.ColName<OrgHistoryDal>(x => x.EntityType)} text NOT NULL,
                    {EntityMapper.ColName<OrgHistoryDal>(x => x.ActorId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<UserProfileDal>()} ({EntityMapper.ColName<UserProfileDal>(x => x.UserId)}),
                    {EntityMapper.ColName<OrgHistoryDal>(x => x.RecordedAt)} timestamptz NOT NULL DEFAULT clock_timestamp(),
                    {EntityMapper.ColName<OrgHistoryDal>(x => x.Snapshot)} jsonb NOT NULL
                )
                """;
            Execute.Sql(createTableQuery);
        }

        if (!exists || !table.Index(HistoryEntity).Exists())
        {
            var createHistoryEntityQuery = $"""
                CREATE INDEX {EntityMapper.Identifier(HistoryEntity)} ON {EntityMapper.TbName<OrgHistoryDal>()} ({EntityMapper.ColName<OrgHistoryDal>(x => x.EntityType)}, {EntityMapper.ColName<OrgHistoryDal>(x => x.EntityId)}, {EntityMapper.ColName<OrgHistoryDal>(x => x.RecordedAt)})
                """;
            Execute.Sql(createHistoryEntityQuery);
        }
    }
}
