using Core.Database.DapperExtensions.EntityMapper;
using Domain.DalModels;
using FluentMigrator;

namespace Infrastructure.Migrations;

/// <summary>Создаёт долговременные задания, строки и начальные версии отчётных источников</summary>
[Migration(202611140800)]
public sealed class Date_202611140800_AddDocuments : Migration
{
    /// <summary>Причина остановки отката после появления новых данных</summary>
    private const string RestoreRequired = "Для отката файлов и отчётов восстановите резервную копию";

    /// <summary>Индекс для исторической выборки по типу, записи и моменту</summary>
    private const string VersionIndex = "IX_OrgHistory_ReportSource";

    /// <summary>Индекс очереди выдаёт задания в порядке регистрации</summary>
    private const string QueueIndex = "IX_DocumentJob_Queue";

    /// <inheritdoc/>
    public override void Up()
    {
        if (!Schema.Table(EntityMapper.TbName<DocumentJobDal>(false)).Exists())
        {
            var query = $"""
                CREATE TABLE {EntityMapper.TbName<DocumentJobDal>()} (
                    {EntityMapper.ColName<DocumentJobDal>(x => x.Id)} uuid NOT NULL,
                    {EntityMapper.ColName<DocumentJobDal>(x => x.OperationId)} uuid NOT NULL,
                    {EntityMapper.ColName<DocumentJobDal>(x => x.OwnerId)} uuid NOT NULL,
                    {EntityMapper.ColName<DocumentJobDal>(x => x.SessionId)} uuid NOT NULL,
                    {EntityMapper.ColName<DocumentJobDal>(x => x.AccessVersion)} bigint NOT NULL,
                    {EntityMapper.ColName<DocumentJobDal>(x => x.Kind)} text NOT NULL,
                    {EntityMapper.ColName<DocumentJobDal>(x => x.State)} text NOT NULL,
                    {EntityMapper.ColName<DocumentJobDal>(x => x.Parameters)} jsonb NOT NULL,
                    {EntityMapper.ColName<DocumentJobDal>(x => x.Mapping)} jsonb NULL,
                    {EntityMapper.ColName<DocumentJobDal>(x => x.Size)} bigint NULL,
                    {EntityMapper.ColName<DocumentJobDal>(x => x.Checksum)} text NULL,
                    {EntityMapper.ColName<DocumentJobDal>(x => x.CreatedAt)} timestamptz NOT NULL,
                    {EntityMapper.ColName<DocumentJobDal>(x => x.CapturedAt)} timestamptz NULL,
                    {EntityMapper.ColName<DocumentJobDal>(x => x.GeneratedAt)} timestamptz NULL,
                    {EntityMapper.ColName<DocumentJobDal>(x => x.CompletedAt)} timestamptz NULL,
                    {EntityMapper.ColName<DocumentJobDal>(x => x.Error)} text NULL,
                    PRIMARY KEY ({EntityMapper.ColName<DocumentJobDal>(x => x.Id)}),
                    UNIQUE ({EntityMapper.ColName<DocumentJobDal>(x => x.OwnerId)}, {EntityMapper.ColName<DocumentJobDal>(x => x.Kind)}, {EntityMapper.ColName<DocumentJobDal>(x => x.OperationId)})
                )
                """;
            Execute.Sql(query);
        }

        if (!Schema.Table(EntityMapper.TbName<DocumentJobDal>(false)).Index(QueueIndex).Exists())
        {
            Create.Index(QueueIndex).OnTable(EntityMapper.TbName<DocumentJobDal>(false))
                .OnColumn(EntityMapper.ColName<DocumentJobDal>(x => x.State, false)).Ascending()
                .OnColumn(EntityMapper.ColName<DocumentJobDal>(x => x.CreatedAt, false)).Ascending()
                .OnColumn(EntityMapper.ColName<DocumentJobDal>(x => x.Id, false)).Ascending();
        }

        if (!Schema.Table(EntityMapper.TbName<DocumentRowDal>(false)).Exists())
        {
            var query = $"""
                CREATE TABLE {EntityMapper.TbName<DocumentRowDal>()} (
                    {EntityMapper.ColName<DocumentRowDal>(x => x.JobId)} uuid NOT NULL,
                    {EntityMapper.ColName<DocumentRowDal>(x => x.Number)} bigint NOT NULL,
                    {EntityMapper.ColName<DocumentRowDal>(x => x.Data)} jsonb NOT NULL,
                    {EntityMapper.ColName<DocumentRowDal>(x => x.HasError)} boolean NOT NULL,
                    PRIMARY KEY ({EntityMapper.ColName<DocumentRowDal>(x => x.JobId)}, {EntityMapper.ColName<DocumentRowDal>(x => x.Number)}),
                    FOREIGN KEY ({EntityMapper.ColName<DocumentRowDal>(x => x.JobId)}) REFERENCES {EntityMapper.TbName<DocumentJobDal>()} ({EntityMapper.ColName<DocumentJobDal>(x => x.Id)})
                )
                """;
            Execute.Sql(query);
        }

        var versions = Schema.Table(EntityMapper.TbName<OrgHistoryDal>(false));
        if (versions.Exists() && !versions.Index(VersionIndex).Exists())
        {
            Create.Index(VersionIndex).OnTable(EntityMapper.TbName<OrgHistoryDal>(false))
                .OnColumn(EntityMapper.ColName<OrgHistoryDal>(x => x.EntityType, false)).Ascending()
                .OnColumn(EntityMapper.ColName<OrgHistoryDal>(x => x.EntityId, false)).Ascending()
                .OnColumn(EntityMapper.ColName<OrgHistoryDal>(x => x.RecordedAt, false)).Descending();
        }

        Baseline<RiskDal>(EntityMapper.ColName<RiskDal>(x => x.Id), EntityMapper.ColName<RiskDal>(x => x.Version));
        Baseline<RiskProcessDal>(EntityMapper.ColName<RiskProcessDal>(x => x.Id), EntityMapper.ColName<RiskProcessDal>(x => x.Version));
        Baseline<RiskAssessmentDal>(EntityMapper.ColName<RiskAssessmentDal>(x => x.Id), EntityMapper.ColName<RiskAssessmentDal>(x => x.Version));
        Baseline<TreatmentDal>(EntityMapper.ColName<TreatmentDal>(x => x.Id), EntityMapper.ColName<TreatmentDal>(x => x.Version));
        Baseline<HazardDal>(EntityMapper.ColName<HazardDal>(x => x.Id), EntityMapper.ColName<HazardDal>(x => x.Version));
        Baseline<HazardGroupDal>(EntityMapper.ColName<HazardGroupDal>(x => x.Id), EntityMapper.ColName<HazardGroupDal>(x => x.Version));
        Baseline<RiskCategoryDal>(EntityMapper.ColName<RiskCategoryDal>(x => x.Id), EntityMapper.ColName<RiskCategoryDal>(x => x.Version));
        Baseline<ProcessDal>(EntityMapper.ColName<ProcessDal>(x => x.Id), EntityMapper.ColName<ProcessDal>(x => x.Version));
        Baseline<DirectorateDal>(EntityMapper.ColName<DirectorateDal>(x => x.Id), EntityMapper.ColName<DirectorateDal>(x => x.Version));
        Baseline<AssetDal>(EntityMapper.ColName<AssetDal>(x => x.Id), EntityMapper.ColName<AssetDal>(x => x.Version));
    }

    /// <summary>Дополняет историю текущей версией на момент установки, сохраняя существующие даты</summary>
    /// <param name="id">Колонка идентификатора источника</param>
    /// <param name="version">Колонка версии источника</param>
    /// <typeparam name="T">Отчётный источник</typeparam>
    private void Baseline<T>(string id, string version)
    {
        if (Schema.Table(EntityMapper.TbName<T>(false)).Exists() && Schema.Table(EntityMapper.TbName<OrgHistoryDal>(false)).Exists())
        {
            var query = $"""
                INSERT INTO {EntityMapper.TbName<OrgHistoryDal>()}
                    ({EntityMapper.ColName<OrgHistoryDal>(x => x.Id)}, {EntityMapper.ColName<OrgHistoryDal>(x => x.EntityId)},
                     {EntityMapper.ColName<OrgHistoryDal>(x => x.EntityType)}, {EntityMapper.ColName<OrgHistoryDal>(x => x.ActorId)},
                     {EntityMapper.ColName<OrgHistoryDal>(x => x.Snapshot)})
                SELECT source.{version}, source.{id}, '{EntityMapper.TbName<T>(false)}',
                    '{Application.Constants.IdentityAccounts.SystemUserId}'::uuid, to_jsonb(source)
                FROM {EntityMapper.TbName<T>()} source
                ON CONFLICT ({EntityMapper.ColName<OrgHistoryDal>(x => x.Id)}) DO NOTHING
                """;
            Execute.Sql(query);
        }
    }

    /// <inheritdoc/>
    public override void Down()
    {
        throw new NotSupportedException(RestoreRequired);
    }
}
