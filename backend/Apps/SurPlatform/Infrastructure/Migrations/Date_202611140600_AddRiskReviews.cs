using Application.Constants;
using Core.Database.DapperExtensions.EntityMapper;
using Domain.DalModels;
using FluentMigrator;

namespace Infrastructure.Migrations;

/// <summary>Хранит настройки, запуски и жизненный цикл пересмотра</summary>
[Migration(202611140600)]
public sealed class Date_202611140600_AddRiskReviews : Migration
{
    /// <summary>Защищает единственный открытый пересмотр</summary>
    private const string OpenIndex = "UX_RiskReview_Waiting";

    /// <summary>Индекс карточки и истории пересмотров</summary>
    private const string RiskHistoryIndex = "IX_RiskReview_History";

    /// <summary>Индекс фоновых незавершённых запусков</summary>
    private const string ProcessingIndex = "IX_ReviewRun_Processing";

    /// <summary>Требование к безопасному откату данных пересмотра</summary>
    private const string RestoreRequired = "Откат схемы пересмотра выполняется восстановлением резервной копии";

    /// <inheritdoc/>
    public override void Up()
    {
        var reviewScheduleExists = Schema.Table(EntityMapper.TbName<ReviewScheduleDal>(false)).Exists();
        if (!reviewScheduleExists)
        {
            var query = $"""
                CREATE TABLE {EntityMapper.TbName<ReviewScheduleDal>()} (
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.Id)} uuid NOT NULL PRIMARY KEY,
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.Version)} uuid NOT NULL,
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.Name)} text NOT NULL,
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.IsActive)} boolean NOT NULL,
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.ResponsibleUserId)} uuid NOT NULL,
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.CreatedById)} uuid NOT NULL,
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.UpdatedById)} uuid NOT NULL,
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.CreatedAt)} timestamptz NOT NULL,
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.UpdatedAt)} timestamptz NOT NULL,
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.TimeZone)} text NOT NULL,
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.StartDate)} timestamp NOT NULL,
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.RecurrenceType)} text NOT NULL,
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.RecurrenceInterval)} integer NULL,
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.Weekdays)} integer[] NOT NULL,
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.EndType)} text NOT NULL,
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.OccurrenceCount)} integer NULL,
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.EndDate)} timestamp NULL
                )
                """;
            Execute.Sql(query);
        }

        var reviewWebhookExists = Schema.Table(EntityMapper.TbName<ReviewWebhookDal>(false)).Exists();
        if (!reviewWebhookExists)
        {
            var query = $"""
                CREATE TABLE {EntityMapper.TbName<ReviewWebhookDal>()} (
                    {EntityMapper.ColName<ReviewWebhookDal>(x => x.Id)} uuid NOT NULL PRIMARY KEY,
                    {EntityMapper.ColName<ReviewWebhookDal>(x => x.Version)} uuid NOT NULL,
                    {EntityMapper.ColName<ReviewWebhookDal>(x => x.Name)} text NOT NULL,
                    {EntityMapper.ColName<ReviewWebhookDal>(x => x.IsActive)} boolean NOT NULL,
                    {EntityMapper.ColName<ReviewWebhookDal>(x => x.ResponsibleUserId)} uuid NOT NULL,
                    {EntityMapper.ColName<ReviewWebhookDal>(x => x.CreatedById)} uuid NOT NULL,
                    {EntityMapper.ColName<ReviewWebhookDal>(x => x.UpdatedById)} uuid NOT NULL,
                    {EntityMapper.ColName<ReviewWebhookDal>(x => x.CreatedAt)} timestamptz NOT NULL,
                    {EntityMapper.ColName<ReviewWebhookDal>(x => x.UpdatedAt)} timestamptz NOT NULL,
                    {EntityMapper.ColName<ReviewWebhookDal>(x => x.ExternalSystemName)} text NOT NULL,
                    {EntityMapper.ColName<ReviewWebhookDal>(x => x.TokenHash)} bytea NOT NULL
                )
                """;
            Execute.Sql(query);
        }

        var reviewWebhookRiskExists = Schema.Table(EntityMapper.TbName<ReviewWebhookRiskDal>(false)).Exists();
        if (!reviewWebhookRiskExists)
        {
            var query = $"""
                CREATE TABLE {EntityMapper.TbName<ReviewWebhookRiskDal>()} (
                    {EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.WebhookId)} uuid NOT NULL,
                    {EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.RiskId)} uuid NOT NULL,
                    {EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.LinkedAt)} timestamptz NOT NULL,
                    {EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.LinkedById)} uuid NOT NULL,
                    FOREIGN KEY ({EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.RiskId)}) REFERENCES {EntityMapper.TbName<RiskDal>()} ({EntityMapper.ColName<RiskDal>(x => x.Id)}),
                    FOREIGN KEY ({EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.WebhookId)}) REFERENCES {EntityMapper.TbName<ReviewWebhookDal>()} ({EntityMapper.ColName<ReviewWebhookDal>(x => x.Id)}) ON DELETE CASCADE,
                    UNIQUE ({EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.WebhookId)}, {EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.RiskId)})
                )
                """;
            Execute.Sql(query);
        }

        var reviewRunExists = Schema.Table(EntityMapper.TbName<ReviewRunDal>(false)).Exists();
        if (!reviewRunExists)
        {
            var query = $"""
                CREATE TABLE {EntityMapper.TbName<ReviewRunDal>()} (
                    {EntityMapper.ColName<ReviewRunDal>(x => x.Id)} uuid NOT NULL PRIMARY KEY,
                    {EntityMapper.ColName<ReviewRunDal>(x => x.Reason)} text NOT NULL,
                    {EntityMapper.ColName<ReviewRunDal>(x => x.SourceType)} text NOT NULL,
                    {EntityMapper.ColName<ReviewRunDal>(x => x.SourceId)} uuid NOT NULL,
                    {EntityMapper.ColName<ReviewRunDal>(x => x.SourceNameSnapshot)} text NULL,
                    {EntityMapper.ColName<ReviewRunDal>(x => x.ExternalSystemNameSnapshot)} text NULL,
                    {EntityMapper.ColName<ReviewRunDal>(x => x.InitiatedById)} uuid NOT NULL,
                    {EntityMapper.ColName<ReviewRunDal>(x => x.ConfigurationAuthorId)} uuid NULL,
                    {EntityMapper.ColName<ReviewRunDal>(x => x.ResponsibleUserId)} uuid NULL,
                    {EntityMapper.ColName<ReviewRunDal>(x => x.TimeZone)} text NULL,
                    {EntityMapper.ColName<ReviewRunDal>(x => x.ScheduledDate)} timestamp NULL,
                    {EntityMapper.ColName<ReviewRunDal>(x => x.OccurredAt)} timestamptz NOT NULL,
                    {EntityMapper.ColName<ReviewRunDal>(x => x.Status)} text NOT NULL,
                    {EntityMapper.ColName<ReviewRunDal>(x => x.RawPayload)} bytea NULL,
                    {EntityMapper.ColName<ReviewRunDal>(x => x.TargetRiskIds)} uuid[] NOT NULL,
                    UNIQUE ({EntityMapper.ColName<ReviewRunDal>(x => x.SourceId)}, {EntityMapper.ColName<ReviewRunDal>(x => x.ScheduledDate)})
                )
                """;
            Execute.Sql(query);
        }

        var riskReviewExists = Schema.Table(EntityMapper.TbName<RiskReviewDal>(false)).Exists();
        if (!riskReviewExists)
        {
            var query = $"""
                CREATE TABLE {EntityMapper.TbName<RiskReviewDal>()} (
                    {EntityMapper.ColName<RiskReviewDal>(x => x.Id)} uuid NOT NULL PRIMARY KEY,
                    {EntityMapper.ColName<RiskReviewDal>(x => x.ReviewRunId)} uuid NOT NULL,
                    {EntityMapper.ColName<RiskReviewDal>(x => x.RiskId)} uuid NOT NULL,
                    {EntityMapper.ColName<RiskReviewDal>(x => x.Status)} text NOT NULL,
                    {EntityMapper.ColName<RiskReviewDal>(x => x.ProcessedAt)} timestamptz NOT NULL,
                    {EntityMapper.ColName<RiskReviewDal>(x => x.ErrorMessage)} text NULL,
                    {EntityMapper.ColName<RiskReviewDal>(x => x.PreviousAssessmentId)} uuid NULL,
                    {EntityMapper.ColName<RiskReviewDal>(x => x.NewAssessmentId)} uuid NULL,
                    {EntityMapper.ColName<RiskReviewDal>(x => x.StartedAt)} timestamptz NULL,
                    {EntityMapper.ColName<RiskReviewDal>(x => x.StartedById)} uuid NULL,
                    {EntityMapper.ColName<RiskReviewDal>(x => x.CompletedAt)} timestamptz NULL,
                    {EntityMapper.ColName<RiskReviewDal>(x => x.CompletedById)} uuid NULL,
                    {EntityMapper.ColName<RiskReviewDal>(x => x.CloseReason)} text NULL,
                    FOREIGN KEY ({EntityMapper.ColName<RiskReviewDal>(x => x.RiskId)}) REFERENCES {EntityMapper.TbName<RiskDal>()} ({EntityMapper.ColName<RiskDal>(x => x.Id)}),
                    FOREIGN KEY ({EntityMapper.ColName<RiskReviewDal>(x => x.ReviewRunId)}) REFERENCES {EntityMapper.TbName<ReviewRunDal>()} ({EntityMapper.ColName<ReviewRunDal>(x => x.Id)}),
                    FOREIGN KEY ({EntityMapper.ColName<RiskReviewDal>(x => x.PreviousAssessmentId)}) REFERENCES {EntityMapper.TbName<RiskAssessmentDal>()} ({EntityMapper.ColName<RiskAssessmentDal>(x => x.Id)}),
                    FOREIGN KEY ({EntityMapper.ColName<RiskReviewDal>(x => x.NewAssessmentId)}) REFERENCES {EntityMapper.TbName<RiskAssessmentDal>()} ({EntityMapper.ColName<RiskAssessmentDal>(x => x.Id)}),
                    UNIQUE ({EntityMapper.ColName<RiskReviewDal>(x => x.ReviewRunId)}, {EntityMapper.ColName<RiskReviewDal>(x => x.RiskId)})
                )
                """;
            Execute.Sql(query);
        }

        if (!riskReviewExists || !Schema.Table(EntityMapper.TbName<RiskReviewDal>(false)).Index(OpenIndex).Exists())
        {
            var query = $"""
                CREATE UNIQUE INDEX {EntityMapper.Identifier(OpenIndex)} ON {EntityMapper.TbName<RiskReviewDal>()}
                    ({EntityMapper.ColName<RiskReviewDal>(x => x.RiskId)}) WHERE {EntityMapper.ColName<RiskReviewDal>(x => x.Status)} = '{ReviewRules.Waiting}'
                """;
            Execute.Sql(query);
        }

        if (!riskReviewExists || !Schema.Table(EntityMapper.TbName<RiskReviewDal>(false)).Index(RiskHistoryIndex).Exists())
        {
            var query = $"""
                CREATE INDEX {EntityMapper.Identifier(RiskHistoryIndex)} ON {EntityMapper.TbName<RiskReviewDal>()}
                    ({EntityMapper.ColName<RiskReviewDal>(x => x.RiskId)}, {EntityMapper.ColName<RiskReviewDal>(x => x.ProcessedAt)})
                """;
            Execute.Sql(query);
        }

        if (!reviewRunExists || !Schema.Table(EntityMapper.TbName<ReviewRunDal>(false)).Index(ProcessingIndex).Exists())
        {
            var query = $"""
                CREATE INDEX {EntityMapper.Identifier(ProcessingIndex)} ON {EntityMapper.TbName<ReviewRunDal>()}
                    ({EntityMapper.ColName<ReviewRunDal>(x => x.OccurredAt)}) WHERE {EntityMapper.ColName<ReviewRunDal>(x => x.Status)} = '{ReviewRules.Processing}'
                """;
            Execute.Sql(query);
        }

        if (Schema.Table(EntityMapper.TbName<RiskDal>(false)).Column(EntityMapper.ColName<RiskDal>(x => x.Status, false)).Exists()
            && Schema.Table(EntityMapper.TbName<RiskAssessmentDal>(false)).Exists())
        {
            var backfillQuery = $"""
                WITH migrated AS (
                    SELECT k.{EntityMapper.ColName<RiskDal>(x => x.Id)} AS risk_id, k.{EntityMapper.ColName<RiskDal>(x => x.UpdatedAt)} AS occurred_at,
                        gen_random_uuid() AS run_id, assessment.{EntityMapper.ColName<RiskAssessmentDal>(x => x.Id)} AS previous_id
                    FROM {EntityMapper.TbName<RiskDal>()} k
                    LEFT JOIN LATERAL (
                        SELECT {EntityMapper.ColName<RiskAssessmentDal>(x => x.Id)} FROM {EntityMapper.TbName<RiskAssessmentDal>()} WHERE {EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskId)} = k.{EntityMapper.ColName<RiskDal>(x => x.Id)}
                        ORDER BY {EntityMapper.ColName<RiskAssessmentDal>(x => x.CreatedAt)} DESC, {EntityMapper.ColName<RiskAssessmentDal>(x => x.Id)} DESC LIMIT 1
                    ) assessment ON true
                    WHERE k.{EntityMapper.ColName<RiskDal>(x => x.Status)} = '{RiskRules.AwaitingReview}'
                        AND NOT EXISTS (SELECT FROM {EntityMapper.TbName<RiskReviewDal>()} rr WHERE rr.{EntityMapper.ColName<RiskReviewDal>(x => x.RiskId)} = k.{EntityMapper.ColName<RiskDal>(x => x.Id)} AND rr.{EntityMapper.ColName<RiskReviewDal>(x => x.Status)} = '{ReviewRules.Waiting}')
                ), runs AS (
                    INSERT INTO {EntityMapper.TbName<ReviewRunDal>()} ({EntityMapper.ColName<ReviewRunDal>(x => x.Id)}, {EntityMapper.ColName<ReviewRunDal>(x => x.Reason)}, {EntityMapper.ColName<ReviewRunDal>(x => x.SourceType)}, {EntityMapper.ColName<ReviewRunDal>(x => x.SourceId)}, {EntityMapper.ColName<ReviewRunDal>(x => x.InitiatedById)}, {EntityMapper.ColName<ReviewRunDal>(x => x.OccurredAt)}, {EntityMapper.ColName<ReviewRunDal>(x => x.Status)}, {EntityMapper.ColName<ReviewRunDal>(x => x.TargetRiskIds)})
                    SELECT run_id, '{ReviewRules.Manual}', '{ReviewRules.RiskCard}', risk_id,
                        '{IdentityAccounts.SystemUserId}', occurred_at, '{ReviewRules.Completed}', ARRAY[risk_id] FROM migrated
                    RETURNING {EntityMapper.ColName<ReviewRunDal>(x => x.Id)}
                )
                INSERT INTO {EntityMapper.TbName<RiskReviewDal>()} ({EntityMapper.ColName<RiskReviewDal>(x => x.Id)}, {EntityMapper.ColName<RiskReviewDal>(x => x.ReviewRunId)}, {EntityMapper.ColName<RiskReviewDal>(x => x.RiskId)}, {EntityMapper.ColName<RiskReviewDal>(x => x.Status)}, {EntityMapper.ColName<RiskReviewDal>(x => x.ProcessedAt)}, {EntityMapper.ColName<RiskReviewDal>(x => x.PreviousAssessmentId)}, {EntityMapper.ColName<RiskReviewDal>(x => x.StartedAt)}, {EntityMapper.ColName<RiskReviewDal>(x => x.StartedById)})
                SELECT gen_random_uuid(), run_id, risk_id, '{ReviewRules.Waiting}', occurred_at, previous_id, occurred_at,
                    '{IdentityAccounts.SystemUserId}' FROM migrated JOIN runs ON runs.{EntityMapper.ColName<ReviewRunDal>(x => x.Id)} = run_id
                """;
            Execute.Sql(backfillQuery);
        }
    }

    /// <inheritdoc/>
    public override void Down()
    {
        throw new NotSupportedException(RestoreRequired);
    }
}
