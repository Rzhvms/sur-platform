using Core.Database.DapperExtensions.EntityMapper;
using Domain.DalModels;
using FluentMigrator;

namespace Infrastructure.Migrations;

/// <summary>Хранилище настроек, доставки и ошибок Jira</summary>
[Migration(202611140700)]
public sealed class Date_202611140700_AddJira : Migration
{
    /// <summary>Откат выполняется через резервную копию</summary>
    private const string RestoreRequired = "Для отката Jira восстановите резервную копию";

    /// <inheritdoc/>
    public override void Up()
    {
        if (!Schema.Table(EntityMapper.TbName<JiraSettingsDal>(false)).Exists())
        {
            var query = $"""
                CREATE TABLE {EntityMapper.TbName<JiraSettingsDal>()} (
                    {EntityMapper.ColName<JiraSettingsDal>(x => x.Id)} uuid NOT NULL PRIMARY KEY,
                    {EntityMapper.ColName<JiraSettingsDal>(x => x.Version)} uuid NOT NULL,
                    {EntityMapper.ColName<JiraSettingsDal>(x => x.BaseUrl)} text NOT NULL,
                    {EntityMapper.ColName<JiraSettingsDal>(x => x.Project)} text NOT NULL,
                    {EntityMapper.ColName<JiraSettingsDal>(x => x.IssueType)} text NOT NULL,
                    {EntityMapper.ColName<JiraSettingsDal>(x => x.DoneStatus)} text NOT NULL,
                    {EntityMapper.ColName<JiraSettingsDal>(x => x.HoldStatus)} text NULL,
                    {EntityMapper.ColName<JiraSettingsDal>(x => x.CancelStatus)} text NULL,
                    {EntityMapper.ColName<JiraSettingsDal>(x => x.Enabled)} boolean NOT NULL,
                    {EntityMapper.ColName<JiraSettingsDal>(x => x.ResponsibleUserId)} uuid NOT NULL,
                    {EntityMapper.ColName<JiraSettingsDal>(x => x.UpdatedById)} uuid NOT NULL,
                    {EntityMapper.ColName<JiraSettingsDal>(x => x.RestSecret)} text NOT NULL,
                    {EntityMapper.ColName<JiraSettingsDal>(x => x.WebhookHash)} bytea NOT NULL,
                    {EntityMapper.ColName<JiraSettingsDal>(x => x.TimeZone)} text NOT NULL,
                    {EntityMapper.ColName<JiraSettingsDal>(x => x.Executors)} text NOT NULL
                )
                """;
            Execute.Sql(query);
        }

        if (!Schema.Table(EntityMapper.TbName<JiraDeliveryDal>(false)).Exists())
        {
            var query = $"""
                CREATE TABLE {EntityMapper.TbName<JiraDeliveryDal>()} (
                    {EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)} uuid NOT NULL PRIMARY KEY,
                    {EntityMapper.ColName<JiraDeliveryDal>(x => x.State)} text NOT NULL,
                    {EntityMapper.ColName<JiraDeliveryDal>(x => x.StartedAt)} timestamptz NULL,
                    {EntityMapper.ColName<JiraDeliveryDal>(x => x.RetryAt)} timestamptz NULL,
                    {EntityMapper.ColName<JiraDeliveryDal>(x => x.IssueId)} text NULL,
                    {EntityMapper.ColName<JiraDeliveryDal>(x => x.IssueKey)} text NULL,
                    {EntityMapper.ColName<JiraDeliveryDal>(x => x.ObservedAt)} timestamptz NULL,
                    {EntityMapper.ColName<JiraDeliveryDal>(x => x.SyncAt)} timestamptz NULL,
                    {EntityMapper.ColName<JiraDeliveryDal>(x => x.TriggeredBy)} uuid NOT NULL,
                    UNIQUE ({EntityMapper.ColName<JiraDeliveryDal>(x => x.IssueId)}),
                    UNIQUE ({EntityMapper.ColName<JiraDeliveryDal>(x => x.IssueKey)}),
                    FOREIGN KEY ({EntityMapper.ColName<JiraDeliveryDal>(x => x.Id)}) REFERENCES {EntityMapper.TbName<TreatmentDal>()} ({EntityMapper.ColName<TreatmentDal>(x => x.Id)})
                )
                """;
            Execute.Sql(query);
        }

        if (!Schema.Table(EntityMapper.TbName<JiraSignalDal>(false)).Exists())
        {
            var query = $"""
                CREATE TABLE {EntityMapper.TbName<JiraSignalDal>()} (
                    {EntityMapper.ColName<JiraSignalDal>(x => x.Id)} uuid NOT NULL PRIMARY KEY,
                    {EntityMapper.ColName<JiraSignalDal>(x => x.IssueId)} text NOT NULL,
                    {EntityMapper.ColName<JiraSignalDal>(x => x.CreatedAt)} timestamptz NOT NULL,
                    UNIQUE ({EntityMapper.ColName<JiraSignalDal>(x => x.IssueId)})
                )
                """;
            Execute.Sql(query);
        }

        if (!Schema.Table(EntityMapper.TbName<JiraErrorDal>(false)).Exists())
        {
            var query = $"""
                CREATE TABLE {EntityMapper.TbName<JiraErrorDal>()} (
                    {EntityMapper.ColName<JiraErrorDal>(x => x.Id)} uuid NOT NULL PRIMARY KEY,
                    {EntityMapper.ColName<JiraErrorDal>(x => x.TreatmentId)} uuid NULL,
                    {EntityMapper.ColName<JiraErrorDal>(x => x.IssueId)} text NULL,
                    {EntityMapper.ColName<JiraErrorDal>(x => x.Operation)} text NOT NULL,
                    {EntityMapper.ColName<JiraErrorDal>(x => x.Code)} text NOT NULL,
                    {EntityMapper.ColName<JiraErrorDal>(x => x.OccurredAt)} timestamptz NOT NULL,
                    {EntityMapper.ColName<JiraErrorDal>(x => x.ResponsibleUserId)} uuid NOT NULL,
                    {EntityMapper.ColName<JiraErrorDal>(x => x.ConfigurationAuthorId)} uuid NOT NULL
                )
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
