using Core.Database.DapperExtensions.EntityMapper;
using Domain.DalModels;
using FluentMigrator;

namespace Infrastructure.Migrations;

/// <summary>Сохраняет контекст и причину ожидания отправки в Jira</summary>
[Migration(202611140801)]
public sealed class Date_202611140801_AddJiraRetryNotice : Migration
{
    /// <inheritdoc/>
    public override void Up()
    {
        var table = EntityMapper.TbName<JiraDeliveryDal>(false);
        if (!Schema.Table(table).Exists())
        {
            return;
        }

        var fromRisk = EntityMapper.ColName<JiraDeliveryDal>(x => x.TriggeredFromRisk, false);
        if (!Schema.Table(table).Column(fromRisk).Exists())
        {
            Alter.Table(table).AddColumn(fromRisk).AsBoolean().Nullable();
        }

        var retryCode = EntityMapper.ColName<JiraDeliveryDal>(x => x.RetryCode, false);
        if (!Schema.Table(table).Column(retryCode).Exists())
        {
            Alter.Table(table).AddColumn(retryCode).AsString().Nullable();
        }
    }

    /// <inheritdoc/>
    public override void Down()
    {
        var table = EntityMapper.TbName<JiraDeliveryDal>(false);
        if (!Schema.Table(table).Exists())
        {
            return;
        }

        var fromRisk = EntityMapper.ColName<JiraDeliveryDal>(x => x.TriggeredFromRisk, false);
        if (Schema.Table(table).Column(fromRisk).Exists())
        {
            Delete.Column(fromRisk).FromTable(table);
        }

        var retryCode = EntityMapper.ColName<JiraDeliveryDal>(x => x.RetryCode, false);
        if (Schema.Table(table).Column(retryCode).Exists())
        {
            Delete.Column(retryCode).FromTable(table);
        }
    }
}
