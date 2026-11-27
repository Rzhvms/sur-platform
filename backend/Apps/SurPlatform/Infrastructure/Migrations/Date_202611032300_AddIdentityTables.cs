using Core.Database.DapperExtensions.EntityMapper;
using Domain.DalModels;
using FluentMigrator;

namespace Infrastructure.Migrations;

/// <summary>
/// Создание таблиц микросервиса авторизации
/// </summary>
[Migration(202611032300)]
public class Date_202611032300_AddIdentityTables : Migration
{
    private readonly string _userProfileTableName = EntityMapper.TbName<UserProfileDal>(false);
    private readonly string _userCredentialTableName = EntityMapper.TbName<UserCredentialDal>(false);
    private readonly string _roleTableName = EntityMapper.TbName<RoleDal>(false);
    private readonly string _rightTableName = EntityMapper.TbName<RightDal>(false);
    private readonly string _roleRightTableName = EntityMapper.TbName<RoleRightDal>(false);
    private readonly string _userRoleTableName = EntityMapper.TbName<UserRoleDal>(false);
    private readonly string _sessionTableName = EntityMapper.TbName<SessionDal>(false);
    private readonly string _refreshTokenTableName = EntityMapper.TbName<RefreshTokenDal>(false);

    /// <inheritdoc/>
    public override void Up()
    {
        if (!Schema.Table(_userProfileTableName).Exists())
        {
            Create.Table(_userProfileTableName)
                .WithColumn(EntityMapper.ColName<UserProfileDal>(x => x.Id, false)).AsGuid().PrimaryKey().NotNullable()
                .WithColumn(EntityMapper.ColName<UserProfileDal>(x => x.UserId, false)).AsGuid().Unique().NotNullable()
                .WithColumn(EntityMapper.ColName<UserProfileDal>(x => x.FirstName, false)).AsString(50).Nullable()
                .WithColumn(EntityMapper.ColName<UserProfileDal>(x => x.LastName, false)).AsString(50).Nullable()
                .WithColumn(EntityMapper.ColName<UserProfileDal>(x => x.Patronymic, false)).AsString(50).Nullable()
                .WithColumn(EntityMapper.ColName<UserProfileDal>(x => x.Phone, false)).AsString(20).Nullable()
                .WithColumn(EntityMapper.ColName<UserProfileDal>(x => x.Email, false)).AsString(50).Nullable()
                .WithColumn(EntityMapper.ColName<UserProfileDal>(x => x.NormalizedEmail, false)).AsString(50).Nullable()
                .WithColumn(EntityMapper.ColName<UserProfileDal>(x => x.IsActive, false)).AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn(EntityMapper.ColName<UserProfileDal>(x => x.IsEmailConfirmed, false)).AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn(EntityMapper.ColName<UserProfileDal>(x => x.IsPhoneConfirmed, false)).AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn(EntityMapper.ColName<UserProfileDal>(x => x.LockoutUntil, false)).AsDateTime().Nullable()
                .WithColumn(EntityMapper.ColName<UserProfileDal>(x => x.CreatedAt, false)).AsDateTime().NotNullable()
                .WithColumn(EntityMapper.ColName<UserProfileDal>(x => x.UpdatedAt, false)).AsDateTime().NotNullable();
        }

        if (!Schema.Table(_userCredentialTableName).Exists())
        {
            Create.Table(_userCredentialTableName)
                .WithColumn(EntityMapper.ColName<UserCredentialDal>(x => x.Id, false)).AsGuid().PrimaryKey().NotNullable()
                .WithColumn(EntityMapper.ColName<UserCredentialDal>(x => x.UserId, false)).AsGuid().Unique().NotNullable()
                    .ForeignKey(_userProfileTableName, EntityMapper.ColName<UserProfileDal>(x => x.UserId, false))
                    .OnDelete(System.Data.Rule.Cascade)
                .WithColumn(EntityMapper.ColName<UserCredentialDal>(x => x.Password, false)).AsString().NotNullable()
                .WithColumn(EntityMapper.ColName<UserCredentialDal>(x => x.PasswordChangedAt, false)).AsDateTime().NotNullable();
        }

        if (!Schema.Table(_roleTableName).Exists())
        {
            Create.Table(_roleTableName)
                .WithColumn(EntityMapper.ColName<RoleDal>(x => x.Id, false)).AsGuid().PrimaryKey().NotNullable()
                .WithColumn(EntityMapper.ColName<RoleDal>(x => x.Name, false)).AsString(100).Unique().NotNullable()
                .WithColumn(EntityMapper.ColName<RoleDal>(x => x.IsSystem, false)).AsBoolean().NotNullable().WithDefaultValue(false);
        }

        if (!Schema.Table(_rightTableName).Exists())
        {
            Create.Table(_rightTableName)
                .WithColumn(EntityMapper.ColName<RightDal>(x => x.Id, false)).AsGuid().PrimaryKey().NotNullable()
                .WithColumn(EntityMapper.ColName<RightDal>(x => x.Name, false)).AsString(100).Unique().NotNullable()
                .WithColumn(EntityMapper.ColName<RightDal>(x => x.IsSystem, false)).AsBoolean().NotNullable().WithDefaultValue(false);
        }

        if (!Schema.Table(_roleRightTableName).Exists())
        {
            Create.Table(_roleRightTableName)
                .WithColumn(EntityMapper.ColName<RoleRightDal>(x => x.Id, false)).AsGuid().PrimaryKey().NotNullable()
                .WithColumn(EntityMapper.ColName<RoleRightDal>(x => x.RoleId, false)).AsGuid().NotNullable()
                    .ForeignKey(_roleTableName, EntityMapper.ColName<RoleDal>(x => x.Id, false))
                    .OnDelete(System.Data.Rule.Cascade)
                .WithColumn(EntityMapper.ColName<RoleRightDal>(x => x.RightId, false)).AsGuid().NotNullable()
                    .ForeignKey(_rightTableName, EntityMapper.ColName<RightDal>(x => x.Id, false))
                    .OnDelete(System.Data.Rule.Cascade);
        }
        
        if (!Schema.Table(_userRoleTableName).Exists())
        {
            Create.Table(_userRoleTableName)
                .WithColumn(EntityMapper.ColName<UserRoleDal>(x => x.UserId, false)).AsGuid().NotNullable()
                    .ForeignKey(_userProfileTableName, EntityMapper.ColName<UserProfileDal>(x => x.UserId, false))
                    .OnDelete(System.Data.Rule.Cascade)
                .WithColumn(EntityMapper.ColName<UserRoleDal>(x => x.RoleId, false)).AsGuid().NotNullable()
                    .ForeignKey(_roleTableName, EntityMapper.ColName<RoleDal>(x => x.Id, false));
        }

        if (!Schema.Table(_sessionTableName).Exists())
        {
            Create.Table(_sessionTableName)
                .WithColumn(EntityMapper.ColName<SessionDal>(x => x.Id, false)).AsGuid().PrimaryKey().NotNullable()
                .WithColumn(EntityMapper.ColName<SessionDal>(x => x.UserId, false)).AsGuid().NotNullable()
                    .ForeignKey(_userProfileTableName, EntityMapper.ColName<UserProfileDal>(x => x.UserId, false))
                    .OnDelete(System.Data.Rule.Cascade)
                .WithColumn(EntityMapper.ColName<SessionDal>(x => x.CreatedAt, false)).AsDateTime().NotNullable()
                .WithColumn(EntityMapper.ColName<SessionDal>(x => x.UpdatedAt, false)).AsDateTime().NotNullable()
                .WithColumn(EntityMapper.ColName<SessionDal>(x => x.ExpiresAt, false)).AsDateTime().NotNullable()
                .WithColumn(EntityMapper.ColName<SessionDal>(x => x.RevokedAt, false)).AsDateTime().Nullable()
                .WithColumn(EntityMapper.ColName<SessionDal>(x => x.RevokeReason, false)).AsString().Nullable()
                .WithColumn(EntityMapper.ColName<SessionDal>(x => x.DeviceName, false)).AsString().Nullable();
        }

        if (!Schema.Table(_refreshTokenTableName).Exists())
        {
            Create.Table(_refreshTokenTableName)
                .WithColumn(EntityMapper.ColName<RefreshTokenDal>(x => x.Id, false)).AsGuid().PrimaryKey().NotNullable()
                .WithColumn(EntityMapper.ColName<RefreshTokenDal>(x => x.SessionId, false)).AsGuid().NotNullable()
                    .ForeignKey(_sessionTableName, EntityMapper.ColName<SessionDal>(x => x.Id, false))
                    .OnDelete(System.Data.Rule.Cascade)
                .WithColumn(EntityMapper.ColName<RefreshTokenDal>(x => x.TokenHash, false)).AsString(100).Unique().NotNullable()
                .WithColumn(EntityMapper.ColName<RefreshTokenDal>(x => x.CreatedAt, false)).AsDateTime().NotNullable()
                .WithColumn(EntityMapper.ColName<RefreshTokenDal>(x => x.ExpiresAt, false)).AsDateTime().NotNullable()
                .WithColumn(EntityMapper.ColName<RefreshTokenDal>(x => x.RevokedAt, false)).AsDateTime().Nullable();
        }
    }

    /// <inheritdoc/>
    public override void Down()
    {
        if (Schema.Table(_refreshTokenTableName).Exists())
        {
            Delete.Table(_refreshTokenTableName);
        }

        if (Schema.Table(_sessionTableName).Exists())
        {
            Delete.Table(_sessionTableName);
        }

        if (Schema.Table(_userRoleTableName).Exists())
        {
            Delete.Table(_userRoleTableName);
        }

        if (Schema.Table(_roleRightTableName).Exists())
        {
            Delete.Table(_roleRightTableName);
        }

        if (Schema.Table(_rightTableName).Exists())
        {
            Delete.Table(_rightTableName);
        }

        if (Schema.Table(_roleTableName).Exists())
        {
            Delete.Table(_roleTableName);
        }

        if (Schema.Table(_userCredentialTableName).Exists())
        {
            Delete.Table(_userCredentialTableName);
        }

        if (Schema.Table(_userProfileTableName).Exists())
        {
            Delete.Table(_userProfileTableName);
        }
    }
}