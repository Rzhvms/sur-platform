using Application.Ports.Services.Encryption;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using FluentMigrator;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Seeds;

/// <summary>
/// Создание тестового пользователя
/// </summary>
[Migration(202611032302)]
public class AddDefaultUserSeed(IPasswordEncryptionService encryptionService, IConfiguration configuration) : Migration
{
    private const string EmailConfigurationKey = "Bootstrap:Email";
    private const string PasswordConfigurationKey = "Bootstrap:Password";
    private const string AdministratorRoleName = "Administrator";

    private readonly string _userProfileTableName = EntityMapper.TbName<UserProfileDal>(false);
    private readonly string _userCredentialTableName = EntityMapper.TbName<UserCredentialDal>(false);
    private readonly string _userRoleTableName = EntityMapper.TbName<UserRoleDal>(false);
    private readonly string _roleTableName = EntityMapper.TbName<RoleDal>(false);

    /// <inheritdoc/>
    public override void Up()
    {
        if (!Schema.Table(_userProfileTableName).Exists() ||
            !Schema.Table(_userCredentialTableName).Exists() ||
            !Schema.Table(_userRoleTableName).Exists() ||
            !Schema.Table(_roleTableName).Exists())
        {
            throw new InvalidOperationException("Не созданы таблицы пользователей и ролей");
        }

        var email = configuration[EmailConfigurationKey];
        var password = configuration[PasswordConfigurationKey];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Для создания первого пользователя задайте Bootstrap:Email и Bootstrap:Password");
        }

        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var profile = new UserProfileDal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Email = email.Trim(),
            NormalizedEmail = email.Trim().ToLowerInvariant(),
            IsActive = true,
            IsEmailConfirmed = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        Insert.IntoTable(_userProfileTableName).Row(profile);
        Insert.IntoTable(_userCredentialTableName).Row(new UserCredentialDal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Password = encryptionService.HashPassword(password, encryptionService.GenerateSalt()),
            PasswordChangedAt = now
        });

        Execute.WithConnection((connection, transaction) =>
        {
            var roleId = connection.QuerySingle<Guid>(
                $"SELECT {EntityMapper.ColName<RoleDal>(x => x.Id)} FROM {EntityMapper.TbName<RoleDal>()} WHERE {EntityMapper.ColName<RoleDal>(x => x.Name)} = @name",
                new { name = AdministratorRoleName },
                transaction);

            connection.Execute(
                $"INSERT INTO {EntityMapper.TbName<UserRoleDal>()} ({EntityMapper.ColName<UserRoleDal>(x => x.UserId)}, {EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}) VALUES(@userId,@roleId)",
                new { userId, roleId },
                transaction);
        });
    }

    /// <inheritdoc/>
    public override void Down()
    {
        var email = configuration[EmailConfigurationKey];

        if (Schema.Table(_userProfileTableName).Exists() && !string.IsNullOrWhiteSpace(email))
        {
            Delete.FromTable(_userProfileTableName)
                .Row(new { NormalizedEmail = email.Trim().ToLowerInvariant() });
        }
    }
}
