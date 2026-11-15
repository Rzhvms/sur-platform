using Core.Database.DapperExtensions.EntityMapper;
using Domain.DalModels;
using FluentMigrator;

namespace Infrastructure.Migrations;

/// <summary>Сохраняет автора подключения Jira и инициатора восстановления</summary>
[Migration(202611140802)]
public sealed class Date_202611140802_AddJiraAdministration : Migration
{
    /// <inheritdoc/>
    public override void Up()
    {
        var table = EntityMapper.TbName<JiraSettingsDal>(false);
        if (!Schema.Table(table).Exists())
        {
            return;
        }

        var createdBy = EntityMapper.ColName<JiraSettingsDal>(x => x.CreatedById, false);
        if (!Schema.Table(table).Column(createdBy).Exists())
        {
            Alter.Table(table).AddColumn(createdBy).AsGuid().Nullable();
        }

        var restoredBy = EntityMapper.ColName<JiraSettingsDal>(x => x.RestoredById, false);
        if (!Schema.Table(table).Column(restoredBy).Exists())
        {
            Alter.Table(table).AddColumn(restoredBy).AsGuid().Nullable();
        }
    }

    /// <inheritdoc/>
    public override void Down()
    {
        var table = EntityMapper.TbName<JiraSettingsDal>(false);
        if (!Schema.Table(table).Exists())
        {
            return;
        }

        var createdBy = EntityMapper.ColName<JiraSettingsDal>(x => x.CreatedById, false);
        if (Schema.Table(table).Column(createdBy).Exists())
        {
            Delete.Column(createdBy).FromTable(table);
        }

        var restoredBy = EntityMapper.ColName<JiraSettingsDal>(x => x.RestoredById, false);
        if (Schema.Table(table).Column(restoredBy).Exists())
        {
            Delete.Column(restoredBy).FromTable(table);
        }
    }
}
