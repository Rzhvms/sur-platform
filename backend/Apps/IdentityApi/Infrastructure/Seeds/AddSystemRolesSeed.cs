using Application.Constants;
using Core.Database.DapperExtensions.EntityMapper;
using Domain.DalModels;
using FluentMigrator;

namespace Infrastructure.Seeds;

/// <summary>
/// Заполнение таблицы ролей системными значениями по умолчанию.
/// </summary>
[Migration(202611032301)]
public class AddSystemRolesSeed : Migration
{
    private readonly string _roleTableName = EntityMapper.TbName<RoleDal>(false);
    private readonly string _rightTableName = EntityMapper.TbName<RightDal>(false);
    private readonly string _roleRightTableName = EntityMapper.TbName<RoleRightDal>(false);

    /// <inheritdoc/>
    public override void Up()
    {
        if (!Schema.Table(_roleTableName).Exists() ||
            !Schema.Table(_rightTableName).Exists() ||
            !Schema.Table(_roleRightTableName).Exists())
        {
            throw new InvalidOperationException("Не созданы таблицы ролей и прав");
        }

        var administratorId = Guid.NewGuid();

        Insert.IntoTable(_roleTableName).Row(new RoleDal
        {
            Id = administratorId,
            Name = "Administrator",
            IsSystem = true
        });

        Insert.IntoTable(_roleTableName).Row(new RoleDal
        {
            Id = Guid.NewGuid(),
            Name = "Viewer",
            IsSystem = true
        });

        Insert.IntoTable(_roleTableName).Row(new RoleDal
        {
            Id = Guid.NewGuid(),
            Name = "Editor",
            IsSystem = true
        });

        Insert.IntoTable(_rightTableName).Row(new RightDal
        {
            Id = Guid.Parse(IdentityRights.ManageUsers),
            Name = "Управление пользователями",
            IsSystem = true
        });

        Insert.IntoTable(_roleRightTableName).Row(new RoleRightDal
        {
            Id = Guid.NewGuid(),
            RoleId = administratorId,
            RightId = Guid.Parse(IdentityRights.ManageUsers)
        });
    }

    /// <inheritdoc/>
    public override void Down()
    {
        if (Schema.Table(_rightTableName).Exists())
        {
            Delete.FromTable(_rightTableName).Row(new { Id = Guid.Parse(IdentityRights.ManageUsers) });
        }

        if (Schema.Table(_roleTableName).Exists())
        {
            Delete.FromTable(_roleTableName).Row(new { Name = "Administrator" });
            Delete.FromTable(_roleTableName).Row(new { Name = "Editor" });
            Delete.FromTable(_roleTableName).Row(new { Name = "Viewer" });
        }
    }
}
