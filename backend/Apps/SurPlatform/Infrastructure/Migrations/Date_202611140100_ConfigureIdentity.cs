using Infrastructure.Constants;
using static Infrastructure.Constants.InitialIdentityCatalog;
using System.Security.Cryptography;
using Application.Constants;
using Application.Helpers.Encryption;
using Core.Database.DapperExtensions.EntityMapper;
using Domain.DalModels;
using FluentMigrator;

namespace Infrastructure.Migrations;

/// <summary>
/// Добавляет жизненный цикл учётных записей, версию доступа и системные роли
/// </summary>
[Migration(202611140100)]
public sealed class Date_202611140100_ConfigureIdentity : Migration
{
    /// <summary>
    /// Имя системного пользователя
    /// </summary>
    private const string SystemName = "Система";

    /// <summary>
    /// Зарезервированный технический адрес
    /// </summary>
    private const string SystemEmail = "system@sur.invalid";

    /// <summary>
    /// Прежнее право, которое переносится в системную роль администратора
    /// </summary>
    private const string LegacyManageUsers = "8bd340d3-7c3b-49b6-935d-40d9a498ac01";

    /// <summary>
    /// Физический идентификатор строки PostgreSQL для очистки дублей старой схемы
    /// </summary>
    private const string TupleIdentifier = "ctid";

    /// <summary>
    /// Сообщение для операции отката
    /// </summary>
    private const string RestoreRequired = "Восстановите резервную копию перед откатом Identity";

    /// <inheritdoc/>
    public override void Up()
    {
        RequireLegacyTables();
        AddIdentityColumns();
        AddIdentityIndexes();
        SeedCatalog();
        SeedSystemAccount();
    }

    /// <inheritdoc/>
    public override void Down()
    {
        throw new NotSupportedException(RestoreRequired);
    }

    /// <summary>
    /// Останавливает обновление при неполной исходной схеме
    /// </summary>
    /// <exception cref="InvalidOperationException">Отсутствует исходная таблица Identity</exception>
    private void RequireLegacyTables()
    {
        if (!Schema.Table(EntityMapper.TbName<UserProfileDal>(false)).Exists() ||
            !Schema.Table(EntityMapper.TbName<UserCredentialDal>(false)).Exists() ||
            !Schema.Table(EntityMapper.TbName<RoleDal>(false)).Exists() ||
            !Schema.Table(EntityMapper.TbName<RightDal>(false)).Exists() ||
            !Schema.Table(EntityMapper.TbName<RoleRightDal>(false)).Exists() ||
            !Schema.Table(EntityMapper.TbName<UserRoleDal>(false)).Exists() ||
            !Schema.Table(EntityMapper.TbName<SessionDal>(false)).Exists() ||
            !Schema.Table(EntityMapper.TbName<RefreshTokenDal>(false)).Exists())
        {
            throw new InvalidOperationException(LegacySchemaRequired);
        }
    }

    /// <summary>
    /// Ошибка обновления базы без исходной схемы Identity
    /// </summary>
    private const string LegacySchemaRequired = "Для обновления требуется исходная схема Identity";

    /// <summary>
    /// Расширяет существующие таблицы Identity
    /// </summary>
    private void AddIdentityColumns()
    {
        if (Schema.Table(EntityMapper.TbName<UserProfileDal>(false)).Exists())
        {
            if (!Schema.Table(EntityMapper.TbName<UserProfileDal>(false)).Column(EntityMapper.ColName<UserProfileDal>(x => x.FullName, false)).Exists())
            {
                var addFullNameQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<UserProfileDal>()}
                    ADD COLUMN {EntityMapper.ColName<UserProfileDal>(x => x.FullName)} text
                    """;
                Execute.Sql(addFullNameQuery);
            }

            if (!Schema.Table(EntityMapper.TbName<UserProfileDal>(false)).Column(EntityMapper.ColName<UserProfileDal>(x => x.Version, false)).Exists())
            {
                var addVersionQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<UserProfileDal>()}
                    ADD COLUMN {EntityMapper.ColName<UserProfileDal>(x => x.Version)} uuid NOT NULL DEFAULT gen_random_uuid()
                    """;
                Execute.Sql(addVersionQuery);
            }

            if (!Schema.Table(EntityMapper.TbName<UserProfileDal>(false)).Column(EntityMapper.ColName<UserProfileDal>(x => x.AccessVersion, false)).Exists())
            {
                var addAccessVersionQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<UserProfileDal>()}
                    ADD COLUMN {EntityMapper.ColName<UserProfileDal>(x => x.AccessVersion)} bigint NOT NULL DEFAULT 1
                    """;
                Execute.Sql(addAccessVersionQuery);
            }

            if (!Schema.Table(EntityMapper.TbName<UserProfileDal>(false)).Column(EntityMapper.ColName<UserProfileDal>(x => x.MustChangePassword, false)).Exists())
            {
                var addMustChangePasswordQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<UserProfileDal>()}
                    ADD COLUMN {EntityMapper.ColName<UserProfileDal>(x => x.MustChangePassword)} boolean NOT NULL DEFAULT false
                    """;
                Execute.Sql(addMustChangePasswordQuery);
            }

            if (!Schema.Table(EntityMapper.TbName<UserProfileDal>(false)).Column(EntityMapper.ColName<UserProfileDal>(x => x.TemporaryPasswordExpiresAt, false)).Exists())
            {
                var addTemporaryPasswordExpiresAtQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<UserProfileDal>()}
                    ADD COLUMN {EntityMapper.ColName<UserProfileDal>(x => x.TemporaryPasswordExpiresAt)} timestamptz
                    """;
                Execute.Sql(addTemporaryPasswordExpiresAtQuery);
            }

            if (!Schema.Table(EntityMapper.TbName<UserProfileDal>(false)).Column(EntityMapper.ColName<UserProfileDal>(x => x.InvitationStatus, false)).Exists())
            {
                var addInvitationStatusQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<UserProfileDal>()}
                    ADD COLUMN {EntityMapper.ColName<UserProfileDal>(x => x.InvitationStatus)} text
                    """;
                Execute.Sql(addInvitationStatusQuery);
            }

            if (!Schema.Table(EntityMapper.TbName<UserProfileDal>(false)).Column(EntityMapper.ColName<UserProfileDal>(x => x.InvitationVersion, false)).Exists())
            {
                var addInvitationVersionQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<UserProfileDal>()}
                    ADD COLUMN {EntityMapper.ColName<UserProfileDal>(x => x.InvitationVersion)} uuid
                    """;
                Execute.Sql(addInvitationVersionQuery);
            }

            if (!Schema.Table(EntityMapper.TbName<UserProfileDal>(false)).Column(EntityMapper.ColName<UserProfileDal>(x => x.InvitationStartedAt, false)).Exists())
            {
                var addInvitationStartedAtQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<UserProfileDal>()}
                    ADD COLUMN {EntityMapper.ColName<UserProfileDal>(x => x.InvitationStartedAt)} timestamptz
                    """;
                Execute.Sql(addInvitationStartedAtQuery);
            }

            if (!Schema.Table(EntityMapper.TbName<UserProfileDal>(false)).Column(EntityMapper.ColName<UserProfileDal>(x => x.UpdatedBy, false)).Exists())
            {
                var addUpdatedByQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<UserProfileDal>()}
                    ADD COLUMN {EntityMapper.ColName<UserProfileDal>(x => x.UpdatedBy)} uuid
                    """;
                Execute.Sql(addUpdatedByQuery);
            }

            if (!Schema.Table(EntityMapper.TbName<UserProfileDal>(false)).Column(EntityMapper.ColName<UserProfileDal>(x => x.IsFirstAdministrator, false)).Exists())
            {
                var addIsFirstAdministratorQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<UserProfileDal>()}
                    ADD COLUMN {EntityMapper.ColName<UserProfileDal>(x => x.IsFirstAdministrator)} boolean NOT NULL DEFAULT false
                    """;
                Execute.Sql(addIsFirstAdministratorQuery);
            }
        }

        if (Schema.Table(EntityMapper.TbName<RoleDal>(false)).Exists())
        {
            if (!Schema.Table(EntityMapper.TbName<RoleDal>(false)).Column(EntityMapper.ColName<RoleDal>(x => x.Code, false)).Exists())
            {
                var addCodeQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<RoleDal>()}
                    ADD COLUMN {EntityMapper.ColName<RoleDal>(x => x.Code)} text
                    """;
                Execute.Sql(addCodeQuery);
            }

            if (!Schema.Table(EntityMapper.TbName<RoleDal>(false)).Column(EntityMapper.ColName<RoleDal>(x => x.Description, false)).Exists())
            {
                var addDescriptionQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<RoleDal>()}
                    ADD COLUMN {EntityMapper.ColName<RoleDal>(x => x.Description)} text
                    """;
                Execute.Sql(addDescriptionQuery);
            }

            if (!Schema.Table(EntityMapper.TbName<RoleDal>(false)).Column(EntityMapper.ColName<RoleDal>(x => x.IsActive, false)).Exists())
            {
                var addIsActiveQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<RoleDal>()}
                    ADD COLUMN {EntityMapper.ColName<RoleDal>(x => x.IsActive)} boolean NOT NULL DEFAULT true
                    """;
                Execute.Sql(addIsActiveQuery);
            }

            if (!Schema.Table(EntityMapper.TbName<RoleDal>(false)).Column(EntityMapper.ColName<RoleDal>(x => x.Version, false)).Exists())
            {
                var addVersionQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<RoleDal>()}
                    ADD COLUMN {EntityMapper.ColName<RoleDal>(x => x.Version)} uuid NOT NULL DEFAULT gen_random_uuid()
                    """;
                Execute.Sql(addVersionQuery);
            }

            if (!Schema.Table(EntityMapper.TbName<RoleDal>(false)).Column(EntityMapper.ColName<RoleDal>(x => x.CreatedAt, false)).Exists())
            {
                var addCreatedAtQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<RoleDal>()}
                    ADD COLUMN {EntityMapper.ColName<RoleDal>(x => x.CreatedAt)} timestamptz NOT NULL DEFAULT now()
                    """;
                Execute.Sql(addCreatedAtQuery);
            }

            if (!Schema.Table(EntityMapper.TbName<RoleDal>(false)).Column(EntityMapper.ColName<RoleDal>(x => x.UpdatedAt, false)).Exists())
            {
                var addUpdatedAtQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<RoleDal>()}
                    ADD COLUMN {EntityMapper.ColName<RoleDal>(x => x.UpdatedAt)} timestamptz NOT NULL DEFAULT now()
                    """;
                Execute.Sql(addUpdatedAtQuery);
            }
        }

        if (Schema.Table(EntityMapper.TbName<RightDal>(false)).Exists())
        {
            if (!Schema.Table(EntityMapper.TbName<RightDal>(false)).Column(EntityMapper.ColName<RightDal>(x => x.Code, false)).Exists())
            {
                var addCodeQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<RightDal>()}
                    ADD COLUMN {EntityMapper.ColName<RightDal>(x => x.Code)} text
                    """;
                Execute.Sql(addCodeQuery);
            }
        }

        if (Schema.Table(EntityMapper.TbName<UserRoleDal>(false)).Exists())
        {
            if (!Schema.Table(EntityMapper.TbName<UserRoleDal>(false)).Column(EntityMapper.ColName<UserRoleDal>(x => x.IsActive, false)).Exists())
            {
                var addIsActiveQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<UserRoleDal>()}
                    ADD COLUMN {EntityMapper.ColName<UserRoleDal>(x => x.IsActive)} boolean NOT NULL DEFAULT true
                    """;
                Execute.Sql(addIsActiveQuery);
            }

            if (!Schema.Table(EntityMapper.TbName<UserRoleDal>(false)).Column(EntityMapper.ColName<UserRoleDal>(x => x.UpdatedBy, false)).Exists())
            {
                var addUpdatedByQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<UserRoleDal>()}
                    ADD COLUMN {EntityMapper.ColName<UserRoleDal>(x => x.UpdatedBy)} uuid
                    """;
                Execute.Sql(addUpdatedByQuery);
            }

            if (!Schema.Table(EntityMapper.TbName<UserRoleDal>(false)).Column(EntityMapper.ColName<UserRoleDal>(x => x.UpdatedAt, false)).Exists())
            {
                var addUpdatedAtQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<UserRoleDal>()}
                    ADD COLUMN {EntityMapper.ColName<UserRoleDal>(x => x.UpdatedAt)} timestamptz NOT NULL DEFAULT now()
                    """;
                Execute.Sql(addUpdatedAtQuery);
            }
        }

        if (Schema.Table(EntityMapper.TbName<SessionDal>(false)).Exists())
        {
            if (!Schema.Table(EntityMapper.TbName<SessionDal>(false)).Column(EntityMapper.ColName<SessionDal>(x => x.Version, false)).Exists())
            {
                var addVersionQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<SessionDal>()}
                    ADD COLUMN {EntityMapper.ColName<SessionDal>(x => x.Version)} uuid NOT NULL DEFAULT gen_random_uuid()
                    """;
                Execute.Sql(addVersionQuery);
            }
        }

        if (Schema.Table(EntityMapper.TbName<RefreshTokenDal>(false)).Exists())
        {
            if (!Schema.Table(EntityMapper.TbName<RefreshTokenDal>(false)).Column(EntityMapper.ColName<RefreshTokenDal>(x => x.UsedAt, false)).Exists())
            {
                var addUsedAtQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<RefreshTokenDal>()}
                    ADD COLUMN {EntityMapper.ColName<RefreshTokenDal>(x => x.UsedAt)} timestamptz
                    """;
                Execute.Sql(addUsedAtQuery);
            }

            if (!Schema.Table(EntityMapper.TbName<RefreshTokenDal>(false)).Column(EntityMapper.ColName<RefreshTokenDal>(x => x.ReplacedById, false)).Exists())
            {
                var addReplacedByIdQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<RefreshTokenDal>()}
                    ADD COLUMN {EntityMapper.ColName<RefreshTokenDal>(x => x.ReplacedById)} uuid REFERENCES {EntityMapper.TbName<RefreshTokenDal>()} ({EntityMapper.ColName<RefreshTokenDal>(x => x.Id)})
                    """;
                Execute.Sql(addReplacedByIdQuery);
            }
        }

        if (!Schema.Table(EntityMapper.TbName<IdentitySeed>(false)).Exists())
        {
            Create.Table(EntityMapper.TbName<IdentitySeed>(false))
                .WithColumn(EntityMapper.ColName<IdentitySeed>(x => x.Id, false)).AsInt32().PrimaryKey();
        }
    }

    /// <summary>
    /// Устанавливает ограничения уникальности после подготовки данных
    /// </summary>
    private void AddIdentityIndexes()
    {
        if (Schema.Table(EntityMapper.TbName<UserProfileDal>(false)).Column(EntityMapper.ColName<UserProfileDal>(x => x.Email, false)).Exists() && Schema.Table(EntityMapper.TbName<UserProfileDal>(false)).Column(EntityMapper.ColName<UserProfileDal>(x => x.NormalizedEmail, false)).Exists())
        {
            var widenEmailQuery = $"""
                ALTER TABLE {EntityMapper.TbName<UserProfileDal>()} ALTER COLUMN {EntityMapper.ColName<UserProfileDal>(x => x.Email)} TYPE text, ALTER COLUMN {EntityMapper.ColName<UserProfileDal>(x => x.NormalizedEmail)} TYPE text
                """;
            Execute.Sql(widenEmailQuery);
        }

        if (Schema.Table(EntityMapper.TbName<UserProfileDal>(false)).Exists())
        {
            var normalizeProfilesQuery = $"""
                UPDATE {EntityMapper.TbName<UserProfileDal>()} SET {EntityMapper.ColName<UserProfileDal>(x => x.NormalizedEmail)} = lower(btrim({EntityMapper.ColName<UserProfileDal>(x => x.Email)})),
                {EntityMapper.ColName<UserProfileDal>(x => x.FullName)} = concat_ws(' ', {EntityMapper.ColName<UserProfileDal>(x => x.LastName)}, {EntityMapper.ColName<UserProfileDal>(x => x.FirstName)}, {EntityMapper.ColName<UserProfileDal>(x => x.Patronymic)})
                WHERE {EntityMapper.ColName<UserProfileDal>(x => x.FullName)} IS NULL
                """;
            Execute.Sql(normalizeProfilesQuery);
        }

        if (Schema.Table(EntityMapper.TbName<UserProfileDal>(false)).Column(EntityMapper.ColName<UserProfileDal>(x => x.NormalizedEmail, false)).Exists() &&
            !Schema.Table(EntityMapper.TbName<UserProfileDal>(false)).Index(IdentityIndexes.NormalizedEmail).Exists())
        {
            var createNormalizedEmailIndexQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(IdentityIndexes.NormalizedEmail)} ON {EntityMapper.TbName<UserProfileDal>()} ({EntityMapper.ColName<UserProfileDal>(x => x.NormalizedEmail)})
                """;
            Execute.Sql(createNormalizedEmailIndexQuery);
        }

        if (Schema.Table(EntityMapper.TbName<UserProfileDal>(false)).Exists() &&
            !Schema.Table(EntityMapper.TbName<UserProfileDal>(false)).Index(IdentityIndexes.FirstAdministrator).Exists())
        {
            var createFirstAdministratorIndexQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(IdentityIndexes.FirstAdministrator)} ON {EntityMapper.TbName<UserProfileDal>()} ({EntityMapper.ColName<UserProfileDal>(x => x.IsFirstAdministrator)})
                WHERE {EntityMapper.ColName<UserProfileDal>(x => x.IsFirstAdministrator)}
                """;
            Execute.Sql(createFirstAdministratorIndexQuery);
        }

        if (Schema.Table(EntityMapper.TbName<UserRoleDal>(false)).Exists())
        {
            var deduplicateUserRoleQuery = $"""
                DELETE FROM {EntityMapper.TbName<UserRoleDal>()} a USING {EntityMapper.TbName<UserRoleDal>()} b
                WHERE a.{EntityMapper.Identifier(TupleIdentifier)} < b.{EntityMapper.Identifier(TupleIdentifier)} AND (a.{EntityMapper.ColName<UserRoleDal>(x => x.UserId)}, a.{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}) = (b.{EntityMapper.ColName<UserRoleDal>(x => x.UserId)}, b.{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)})
                """;
            Execute.Sql(deduplicateUserRoleQuery);
        }

        if (Schema.Table(EntityMapper.TbName<RoleRightDal>(false)).Exists())
        {
            var deduplicateRoleRightQuery = $"""
                DELETE FROM {EntityMapper.TbName<RoleRightDal>()} a USING {EntityMapper.TbName<RoleRightDal>()} b
                WHERE a.{EntityMapper.ColName<RoleRightDal>(x => x.Id)} < b.{EntityMapper.ColName<RoleRightDal>(x => x.Id)} AND (a.{EntityMapper.ColName<RoleRightDal>(x => x.RoleId)}, a.{EntityMapper.ColName<RoleRightDal>(x => x.RightId)}) = (b.{EntityMapper.ColName<RoleRightDal>(x => x.RoleId)}, b.{EntityMapper.ColName<RoleRightDal>(x => x.RightId)})
                """;
            Execute.Sql(deduplicateRoleRightQuery);
        }

        if (Schema.Table(EntityMapper.TbName<RoleDal>(false)).Exists() &&
            !Schema.Table(EntityMapper.TbName<RoleDal>(false)).Index(IdentityIndexes.RoleCode).Exists())
        {
            var createRoleCodeIndexQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(IdentityIndexes.RoleCode)} ON {EntityMapper.TbName<RoleDal>()} ({EntityMapper.ColName<RoleDal>(x => x.Code)})
                """;
            Execute.Sql(createRoleCodeIndexQuery);
        }

        if (Schema.Table(EntityMapper.TbName<RightDal>(false)).Exists() &&
            !Schema.Table(EntityMapper.TbName<RightDal>(false)).Index(IdentityIndexes.RightCode).Exists())
        {
            var createRightCodeIndexQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(IdentityIndexes.RightCode)} ON {EntityMapper.TbName<RightDal>()} ({EntityMapper.ColName<RightDal>(x => x.Code)})
                """;
            Execute.Sql(createRightCodeIndexQuery);
        }

        if (Schema.Table(EntityMapper.TbName<UserRoleDal>(false)).Column(EntityMapper.ColName<UserRoleDal>(x => x.UserId, false)).Exists() &&
            !Schema.Table(EntityMapper.TbName<UserRoleDal>(false)).Index(IdentityIndexes.UserRole).Exists())
        {
            var createUserRoleIndexQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(IdentityIndexes.UserRole)} ON {EntityMapper.TbName<UserRoleDal>()} ({EntityMapper.ColName<UserRoleDal>(x => x.UserId)}, {EntityMapper.ColName<UserRoleDal>(x => x.RoleId)})
                """;
            Execute.Sql(createUserRoleIndexQuery);
        }

        if (Schema.Table(EntityMapper.TbName<RoleRightDal>(false)).Column(EntityMapper.ColName<RoleRightDal>(x => x.RoleId, false)).Exists() &&
            !Schema.Table(EntityMapper.TbName<RoleRightDal>(false)).Index(IdentityIndexes.RoleRight).Exists())
        {
            var createRoleRightIndexQuery = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(IdentityIndexes.RoleRight)} ON {EntityMapper.TbName<RoleRightDal>()} ({EntityMapper.ColName<RoleRightDal>(x => x.RoleId)}, {EntityMapper.ColName<RoleRightDal>(x => x.RightId)})
                """;
            Execute.Sql(createRoleRightIndexQuery);
        }
    }

    /// <summary>
    /// Заполняет системные роли и права и переносит прежние административные назначения
    /// </summary>
    private void SeedCatalog()
    {
        if (Schema.Table(EntityMapper.TbName<RoleDal>(false)).Exists())
        {
            var upsertRolesQuery = $"""
                WITH catalog(code, name, description) AS (VALUES
                    ('{IdentityAccounts.AdminRole}', '{AdminRoleName}', '{AdminDescription}'),
                    ('{CeoCode}', '{CeoName}', '{CeoDescription}'),
                    ('{RiskSpecialistCode}', '{RiskSpecialistName}', '{RiskSpecialistDescription}'),
                    ('{DirectorCode}', '{DirectorName}', '{DirectorDescription}'),
                    ('{OwnerCode}', '{OwnerName}', '{OwnerDescription}'),
                    ('{EmployeeCode}', '{EmployeeName}', '{EmployeeDescription}')
                )
                INSERT INTO {EntityMapper.TbName<RoleDal>()} ({EntityMapper.ColName<RoleDal>(x => x.Id)},
                    {EntityMapper.ColName<RoleDal>(x => x.Name)}, {EntityMapper.ColName<RoleDal>(x => x.IsSystem)},
                    {EntityMapper.ColName<RoleDal>(x => x.Code)}, {EntityMapper.ColName<RoleDal>(x => x.Description)})
                SELECT gen_random_uuid(), name, true, code, description FROM catalog
                ON CONFLICT ({EntityMapper.ColName<RoleDal>(x => x.Name)}) DO UPDATE
                SET {EntityMapper.ColName<RoleDal>(x => x.Code)} = excluded.{EntityMapper.ColName<RoleDal>(x => x.Code)},
                    {EntityMapper.ColName<RoleDal>(x => x.Description)} = excluded.{EntityMapper.ColName<RoleDal>(x => x.Description)},
                    {EntityMapper.ColName<RoleDal>(x => x.IsSystem)} = true
                """;
            Execute.Sql(upsertRolesQuery);
        }

        if (Schema.Table(EntityMapper.TbName<RightDal>(false)).Exists())
        {
            var upsertRightsQuery = $"""
                WITH catalog(code, name) AS (VALUES
                    ('{AdminCode}', '{AdminName}'),
                    ('{ViewRiskCode}', '{ViewRiskName}'),
                    ('{CommentRiskCode}', '{CommentRiskName}'),
                    ('{CommentTreatmentCode}', '{CommentTreatmentName}'),
                    ('{CreateRiskCode}', '{CreateRiskName}'),
                    ('{EditRiskCode}', '{EditRiskName}'),
                    ('{CloseRiskCode}', '{CloseRiskName}'),
                    ('{AssessRiskCode}', '{AssessRiskName}'),
                    ('{AcceptOwnerRiskCode}', '{AcceptOwnerRiskName}'),
                    ('{AcceptDirectorRiskCode}', '{AcceptDirectorRiskName}'),
                    ('{AcceptCeoRiskCode}', '{AcceptCeoRiskName}'),
                    ('{CreateTreatmentCode}', '{CreateTreatmentName}'),
                    ('{EditTreatmentCode}', '{EditTreatmentName}'),
                    ('{AssignTreatmentExecutorCode}', '{AssignTreatmentExecutorName}'),
                    ('{ExecuteTreatmentCode}', '{ExecuteTreatmentName}'),
                    ('{ApproveOwnerTreatmentCode}', '{ApproveOwnerTreatmentName}'),
                    ('{ApproveDirectorTreatmentCode}', '{ApproveDirectorTreatmentName}'),
                    ('{ApproveCeoTreatmentCode}', '{ApproveCeoTreatmentName}'),
                    ('{ViewReportCode}', '{ViewReportName}'),
                    ('{ExportReportCode}', '{ExportReportName}'),
                    ('{ImportDataCode}', '{ImportDataName}'),
                    ('{SplitRiskCode}', '{SplitRiskName}'),
                    ('{TransferRiskCode}', '{TransferRiskName}')
                )
                INSERT INTO {EntityMapper.TbName<RightDal>()} ({EntityMapper.ColName<RightDal>(x => x.Id)},
                    {EntityMapper.ColName<RightDal>(x => x.Name)}, {EntityMapper.ColName<RightDal>(x => x.IsSystem)},
                    {EntityMapper.ColName<RightDal>(x => x.Code)})
                SELECT CASE WHEN code = '{AdminCode}' THEN '{IdentityRights.Admin}'::uuid ELSE gen_random_uuid() END,
                    name, true, code FROM catalog
                ON CONFLICT ({EntityMapper.ColName<RightDal>(x => x.Name)}) DO UPDATE
                SET {EntityMapper.ColName<RightDal>(x => x.Code)} = excluded.{EntityMapper.ColName<RightDal>(x => x.Code)},
                    {EntityMapper.ColName<RightDal>(x => x.IsSystem)} = true
                """;
            Execute.Sql(upsertRightsQuery);
        }

        if (Schema.Table(EntityMapper.TbName<RoleDal>(false)).Exists() && Schema.Table(EntityMapper.TbName<RightDal>(false)).Exists() && Schema.Table(EntityMapper.TbName<RoleRightDal>(false)).Exists())
        {
            var insertInitialMatrixQuery = $"""
                WITH matrix(role, right_code) AS (VALUES
                    ('{IdentityAccounts.AdminRole}', '{AdminCode}'),
                    ('{EmployeeCode}', '{ViewRiskCode}'),
                    ('{EmployeeCode}', '{CommentRiskCode}'),
                    ('{EmployeeCode}', '{CommentTreatmentCode}'),
                    ('{EmployeeCode}', '{CreateRiskCode}'),
                    ('{EmployeeCode}', '{CreateTreatmentCode}'),
                    ('{EmployeeCode}', '{ExecuteTreatmentCode}'),
                    ('{OwnerCode}', '{ViewRiskCode}'),
                    ('{OwnerCode}', '{CommentRiskCode}'),
                    ('{OwnerCode}', '{CommentTreatmentCode}'),
                    ('{OwnerCode}', '{CreateRiskCode}'),
                    ('{OwnerCode}', '{CreateTreatmentCode}'),
                    ('{OwnerCode}', '{ExecuteTreatmentCode}'),
                    ('{OwnerCode}', '{EditRiskCode}'),
                    ('{OwnerCode}', '{CloseRiskCode}'),
                    ('{OwnerCode}', '{AssessRiskCode}'),
                    ('{OwnerCode}', '{AcceptOwnerRiskCode}'),
                    ('{OwnerCode}', '{EditTreatmentCode}'),
                    ('{OwnerCode}', '{AssignTreatmentExecutorCode}'),
                    ('{OwnerCode}', '{ApproveOwnerTreatmentCode}'),
                    ('{OwnerCode}', '{SplitRiskCode}'),
                    ('{OwnerCode}', '{TransferRiskCode}'),
                    ('{OwnerCode}', '{ViewReportCode}'),
                    ('{OwnerCode}', '{ExportReportCode}'),
                    ('{DirectorCode}', '{ViewRiskCode}'),
                    ('{DirectorCode}', '{CommentRiskCode}'),
                    ('{DirectorCode}', '{CommentTreatmentCode}'),
                    ('{DirectorCode}', '{ViewReportCode}'),
                    ('{DirectorCode}', '{ExportReportCode}'),
                    ('{DirectorCode}', '{AcceptDirectorRiskCode}'),
                    ('{DirectorCode}', '{ApproveDirectorTreatmentCode}'),
                    ('{CeoCode}', '{ViewRiskCode}'),
                    ('{CeoCode}', '{CommentRiskCode}'),
                    ('{CeoCode}', '{CommentTreatmentCode}'),
                    ('{CeoCode}', '{ViewReportCode}'),
                    ('{CeoCode}', '{ExportReportCode}'),
                    ('{CeoCode}', '{AcceptCeoRiskCode}'),
                    ('{CeoCode}', '{ApproveCeoTreatmentCode}'),
                    ('{RiskSpecialistCode}', '{ViewRiskCode}'),
                    ('{RiskSpecialistCode}', '{CommentRiskCode}'),
                    ('{RiskSpecialistCode}', '{CommentTreatmentCode}'),
                    ('{RiskSpecialistCode}', '{ViewReportCode}'),
                    ('{RiskSpecialistCode}', '{ExportReportCode}'),
                    ('{RiskSpecialistCode}', '{AcceptCeoRiskCode}'),
                    ('{RiskSpecialistCode}', '{ApproveCeoTreatmentCode}')
                )
                INSERT INTO {EntityMapper.TbName<RoleRightDal>()} ({EntityMapper.ColName<RoleRightDal>(x => x.Id)},
                    {EntityMapper.ColName<RoleRightDal>(x => x.RoleId)}, {EntityMapper.ColName<RoleRightDal>(x => x.RightId)})
                SELECT gen_random_uuid(), r.{EntityMapper.ColName<RoleDal>(x => x.Id)}, p.{EntityMapper.ColName<RightDal>(x => x.Id)}
                FROM matrix m JOIN {EntityMapper.TbName<RoleDal>()} r ON r.{EntityMapper.ColName<RoleDal>(x => x.Code)} = m.role
                JOIN {EntityMapper.TbName<RightDal>()} p ON p.{EntityMapper.ColName<RightDal>(x => x.Code)} = m.right_code
                WHERE NOT EXISTS (SELECT FROM {EntityMapper.TbName<IdentitySeed>()})
                ON CONFLICT ({EntityMapper.ColName<RoleRightDal>(x => x.RoleId)}, {EntityMapper.ColName<RoleRightDal>(x => x.RightId)}) DO NOTHING
                """;
            Execute.Sql(insertInitialMatrixQuery);
        }

        if (Schema.Table(EntityMapper.TbName<UserRoleDal>(false)).Exists() && Schema.Table(EntityMapper.TbName<RoleRightDal>(false)).Exists() && Schema.Table(EntityMapper.TbName<RoleDal>(false)).Exists())
        {
            var transferLegacyAdminsQuery = $"""
                INSERT INTO {EntityMapper.TbName<UserRoleDal>()} ({EntityMapper.ColName<UserRoleDal>(x => x.UserId)},
                    {EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}, {EntityMapper.ColName<UserRoleDal>(x => x.IsActive)})
                SELECT DISTINCT a.{EntityMapper.ColName<UserRoleDal>(x => x.UserId)}, r.{EntityMapper.ColName<RoleDal>(x => x.Id)}, true
                FROM {EntityMapper.TbName<UserRoleDal>()} a
                JOIN {EntityMapper.TbName<RoleRightDal>()} rr
                    ON rr.{EntityMapper.ColName<RoleRightDal>(x => x.RoleId)} = a.{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}
                CROSS JOIN {EntityMapper.TbName<RoleDal>()} r
                WHERE NOT EXISTS (SELECT FROM {EntityMapper.TbName<IdentitySeed>()})
                    AND a.{EntityMapper.ColName<UserRoleDal>(x => x.IsActive)} AND r.{EntityMapper.ColName<RoleDal>(x => x.Code)} = '{IdentityAccounts.AdminRole}'
                    AND rr.{EntityMapper.ColName<RoleRightDal>(x => x.RightId)} = '{LegacyManageUsers}'
                ON CONFLICT ({EntityMapper.ColName<UserRoleDal>(x => x.UserId)}, {EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}) DO NOTHING
                """;
            Execute.Sql(transferLegacyAdminsQuery);
        }

        // Таблица маркера уже существует или создаётся предыдущим выражением миграции
        var recordSeedQuery = $"""
            INSERT INTO {EntityMapper.TbName<IdentitySeed>()} ({EntityMapper.ColName<IdentitySeed>(x => x.Id)})
            VALUES (1) ON CONFLICT DO NOTHING
            """;
        Execute.Sql(recordSeedQuery);
    }

    /// <summary>
    /// Создаёт технического пользователя и недоступный для входа пароль
    /// </summary>
    private void SeedSystemAccount()
    {
        if (Schema.Table(EntityMapper.TbName<UserProfileDal>(false)).Exists())
        {
            var insertSystemProfileQuery = $"""
                INSERT INTO {EntityMapper.TbName<UserProfileDal>()}
                    ({EntityMapper.ColName<UserProfileDal>(x => x.Id)}, {EntityMapper.ColName<UserProfileDal>(x => x.UserId)}, {EntityMapper.ColName<UserProfileDal>(x => x.FullName)}, {EntityMapper.ColName<UserProfileDal>(x => x.Email)}, {EntityMapper.ColName<UserProfileDal>(x => x.NormalizedEmail)}, {EntityMapper.ColName<UserProfileDal>(x => x.IsActive)}, {EntityMapper.ColName<UserProfileDal>(x => x.CreatedAt)}, {EntityMapper.ColName<UserProfileDal>(x => x.UpdatedAt)}, {EntityMapper.ColName<UserProfileDal>(x => x.UpdatedBy)})
                VALUES ('{IdentityAccounts.SystemUserId}', '{IdentityAccounts.SystemUserId}', '{SystemName}', '{SystemEmail}', '{SystemEmail}',
                    false, now(), now(), '{IdentityAccounts.SystemUserId}') ON CONFLICT ({EntityMapper.ColName<UserProfileDal>(x => x.UserId)}) DO NOTHING
                """;
            Execute.Sql(insertSystemProfileQuery);
        }

        if (Schema.Table(EntityMapper.TbName<UserCredentialDal>(false)).Exists())
        {
            var systemPasswordHash = PasswordEncryptionHelper.HashPassword(
                Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)), PasswordEncryptionHelper.GenerateSalt());
            var insertSystemCredentialQuery = $"""
                INSERT INTO {EntityMapper.TbName<UserCredentialDal>()} ({EntityMapper.ColName<UserCredentialDal>(x => x.Id)}, {EntityMapper.ColName<UserCredentialDal>(x => x.UserId)}, {EntityMapper.ColName<UserCredentialDal>(x => x.Password)}, {EntityMapper.ColName<UserCredentialDal>(x => x.PasswordChangedAt)})
                VALUES (gen_random_uuid(), '{IdentityAccounts.SystemUserId}',
                    '{systemPasswordHash}', now()) ON CONFLICT ({EntityMapper.ColName<UserCredentialDal>(x => x.UserId)}) DO NOTHING
                """;
            Execute.Sql(insertSystemCredentialQuery);
        }
    }
}
