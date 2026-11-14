using Application.Constants;
using Core.Database.DapperExtensions.EntityMapper;
using Domain.DalModels;
using FluentMigrator;

namespace Infrastructure.Migrations;

/// <summary>Добавляет версионируемую шкалу, пороги и снимки расчёта</summary>
[Migration(202611140400)]
public sealed class Date_202611140400_AddRiskAssessment : Migration
{
    /// <summary>Сообщение об откате через восстановление базы</summary>
    private const string RestoreRequired = "Откат схемы оценок выполняется восстановлением резервной копии";

    /// <inheritdoc/>
    public override void Up()
    {
        if (!Schema.Table(EntityMapper.TbName<ImpactScaleDal>(false)).Exists())
        {
            var createQuery = $"""
                CREATE TABLE {EntityMapper.TbName<ImpactScaleDal>()} (
                    {EntityMapper.ColName<ImpactScaleDal>(x => x.Id)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<ImpactScaleDal>(x => x.Sequence)} bigint GENERATED ALWAYS AS IDENTITY UNIQUE,
                    {EntityMapper.ColName<ImpactScaleDal>(x => x.CreatedById)} uuid NOT NULL REFERENCES {EntityMapper.TbName<UserProfileDal>()} ({EntityMapper.ColName<UserProfileDal>(x => x.UserId)}),
                    {EntityMapper.ColName<ImpactScaleDal>(x => x.CreatedAt)} timestamptz NOT NULL DEFAULT clock_timestamp()
                )
                """;
            Execute.Sql(createQuery);
        }

        if (!Schema.Table(EntityMapper.TbName<ImpactThresholdDal>(false)).Exists())
        {
            var createQuery = $"""
                CREATE TABLE {EntityMapper.TbName<ImpactThresholdDal>()} (
                    {EntityMapper.ColName<ImpactThresholdDal>(x => x.ScaleId)} uuid NOT NULL REFERENCES {EntityMapper.TbName<ImpactScaleDal>()} ({EntityMapper.ColName<ImpactScaleDal>(x => x.Id)}),
                    {EntityMapper.ColName<ImpactThresholdDal>(x => x.Weight)} integer NOT NULL,
                    {EntityMapper.ColName<ImpactThresholdDal>(x => x.MinLossAmount)} numeric NOT NULL,
                    {EntityMapper.ColName<ImpactThresholdDal>(x => x.MaxLossAmount)} numeric,
                    PRIMARY KEY ({EntityMapper.ColName<ImpactThresholdDal>(x => x.ScaleId)}, {EntityMapper.ColName<ImpactThresholdDal>(x => x.Weight)})
                )
                """;
            Execute.Sql(createQuery);
        }

        if (!Schema.Table(EntityMapper.TbName<RiskAcceptanceSettingsDal>(false)).Exists())
        {
            var createQuery = $"""
                CREATE TABLE {EntityMapper.TbName<RiskAcceptanceSettingsDal>()} (
                    {EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.Id)} uuid PRIMARY KEY,
                    {EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.Version)} uuid NOT NULL DEFAULT gen_random_uuid(),
                    {EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.OwnerMaxScore)} integer NOT NULL,
                    {EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.DirectorMaxScore)} integer NOT NULL,
                    {EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.CeoMaxScore)} integer NOT NULL,
                    {EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.UpdatedById)} uuid REFERENCES {EntityMapper.TbName<UserProfileDal>()} ({EntityMapper.ColName<UserProfileDal>(x => x.UserId)}),
                    {EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.UpdatedAt)} timestamptz NOT NULL DEFAULT clock_timestamp()
                )
                """;
            Execute.Sql(createQuery);
        }

        var settingsId = AssessmentMethod.AcceptanceSettingsId;
        var seedQuery = $"""
            INSERT INTO {EntityMapper.TbName<RiskAcceptanceSettingsDal>()}
                ({EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.Id)}, {EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.OwnerMaxScore)}, {EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.DirectorMaxScore)}, {EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.CeoMaxScore)})
            VALUES ('{settingsId}', {AssessmentMethod.DefaultOwnerMax}, {AssessmentMethod.DefaultDirectorMax}, {AssessmentMethod.DefaultCeoMax})
            ON CONFLICT ({EntityMapper.ColName<RiskAcceptanceSettingsDal>(x => x.Id)}) DO NOTHING
            """;
        Execute.Sql(seedQuery);

        var assessments = Schema.Table(EntityMapper.TbName<RiskAssessmentDal>(false));
        if (assessments.Exists())
        {
            if (!assessments.Column(EntityMapper.ColName<RiskAssessmentDal>(x => x.MethodId, false)).Exists())
            {
                var addColumnQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<RiskAssessmentDal>()} ADD COLUMN {EntityMapper.ColName<RiskAssessmentDal>(x => x.MethodId)} uuid
                    """;
                Execute.Sql(addColumnQuery);
            }

            if (!assessments.Column(EntityMapper.ColName<RiskAssessmentDal>(x => x.ScaleVersionId, false)).Exists())
            {
                var addColumnQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<RiskAssessmentDal>()} ADD COLUMN {EntityMapper.ColName<RiskAssessmentDal>(x => x.ScaleVersionId)} uuid REFERENCES {EntityMapper.TbName<ImpactScaleDal>()} ({EntityMapper.ColName<ImpactScaleDal>(x => x.Id)})
                    """;
                Execute.Sql(addColumnQuery);
            }

            if (!assessments.Column(EntityMapper.ColName<RiskAssessmentDal>(x => x.CreatedById, false)).Exists())
            {
                var addColumnQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<RiskAssessmentDal>()} ADD COLUMN {EntityMapper.ColName<RiskAssessmentDal>(x => x.CreatedById)} uuid REFERENCES {EntityMapper.TbName<UserProfileDal>()} ({EntityMapper.ColName<UserProfileDal>(x => x.UserId)})
                    """;
                Execute.Sql(addColumnQuery);
            }

            if (!assessments.Column(EntityMapper.ColName<RiskAssessmentDal>(x => x.LossAmount, false)).Exists())
            {
                var addColumnQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<RiskAssessmentDal>()} ADD COLUMN {EntityMapper.ColName<RiskAssessmentDal>(x => x.LossAmount)} numeric
                    """;
                Execute.Sql(addColumnQuery);
            }

            if (!assessments.Column(EntityMapper.ColName<RiskAssessmentDal>(x => x.DowntimeWeeks, false)).Exists())
            {
                var addColumnQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<RiskAssessmentDal>()} ADD COLUMN {EntityMapper.ColName<RiskAssessmentDal>(x => x.DowntimeWeeks)} integer
                    """;
                Execute.Sql(addColumnQuery);
            }

            if (!assessments.Column(EntityMapper.ColName<RiskAssessmentDal>(x => x.DowntimeDays, false)).Exists())
            {
                var addColumnQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<RiskAssessmentDal>()} ADD COLUMN {EntityMapper.ColName<RiskAssessmentDal>(x => x.DowntimeDays)} integer
                    """;
                Execute.Sql(addColumnQuery);
            }

            if (!assessments.Column(EntityMapper.ColName<RiskAssessmentDal>(x => x.DowntimeHours, false)).Exists())
            {
                var addColumnQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<RiskAssessmentDal>()} ADD COLUMN {EntityMapper.ColName<RiskAssessmentDal>(x => x.DowntimeHours)} numeric
                    """;
                Execute.Sql(addColumnQuery);
            }

            if (!assessments.Column(EntityMapper.ColName<RiskAssessmentDal>(x => x.DowntimeTotalHours, false)).Exists())
            {
                var addColumnQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<RiskAssessmentDal>()} ADD COLUMN {EntityMapper.ColName<RiskAssessmentDal>(x => x.DowntimeTotalHours)} numeric
                    """;
                Execute.Sql(addColumnQuery);
            }

            if (!assessments.Column(EntityMapper.ColName<RiskAssessmentDal>(x => x.HourlyDowntimeCostSnapshot, false)).Exists())
            {
                var addColumnQuery = $"""
                    ALTER TABLE {EntityMapper.TbName<RiskAssessmentDal>()} ADD COLUMN {EntityMapper.ColName<RiskAssessmentDal>(x => x.HourlyDowntimeCostSnapshot)} numeric
                    """;
                Execute.Sql(addColumnQuery);
            }
        }
    }

    /// <inheritdoc/>
    public override void Down()
    {
        throw new NotSupportedException(RestoreRequired);
    }
}
