using System.Data;
using Application.Constants;
using Application.Exceptions;
using Application.UseCases.Documents.Dto.Request;
using Application.UseCases.Risks.Dto;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;

namespace Infrastructure.Repositories.Risks;

/// <summary>Материализация отчётов в одном снимке PostgreSQL</summary>
internal sealed partial class RiskRepository
{
    /// <summary>Копирует ожидающие и обработанные события административной истории</summary>
    /// <param name="job">Сохранённый запрос журнала</param>
    /// <param name="transaction">Согласованный снимок источника</param>
    private async Task CaptureHistoryDocumentAsync(DocumentJobDal job, IDbTransaction transaction)
    {
        var (from, until) = Boundaries(ReadParameters<BusinessPeriod>(job));
        var id = job.Id;
        var query = $"""
            INSERT INTO {EntityMapper.TbName<DocumentRowDal>()} ({EntityMapper.ColName<DocumentRowDal>(x => x.JobId)},
                {EntityMapper.ColName<DocumentRowDal>(x => x.Number)}, {EntityMapper.ColName<DocumentRowDal>(x => x.Data)},
                {EntityMapper.ColName<DocumentRowDal>(x => x.HasError)})
            SELECT {EntityMapper.ParameterName(nameof(id))}, row_number() OVER (
                ORDER BY {EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)},
                         {EntityMapper.ColName<BusinessChangeDal>(x => x.EventId)}),
                {EntityMapper.ColName<BusinessChangeDal>(x => x.Payload)}, false
            FROM ({JournalSource}) events
            WHERE {EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)} >= {EntityMapper.ParameterName(nameof(from))}
              AND {EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)} < {EntityMapper.ParameterName(nameof(until))}
            ORDER BY {EntityMapper.ColName<BusinessChangeDal>(x => x.OccurredAt)},
                     {EntityMapper.ColName<BusinessChangeDal>(x => x.EventId)}
            LIMIT {DocumentRules.RowLimit + 1}
            """;
        if (await connection.ExecuteAsync(query, new { id, from, until }, transaction) > DocumentRules.RowLimit)
        {
            throw new IdentityCommandException(400, DocumentRules.Invalid);
        }
    }

    /// <summary>Записывает риски, оценки и меры вместе с историческими названиями</summary>
    /// <param name="job">Сохранённые фильтры отчёта</param>
    /// <param name="captured">Время согласованной выборки</param>
    /// <param name="transaction">Снимок версий и текущих таблиц</param>
    private async Task CaptureRiskReportAsync(DocumentJobDal job, DateTime captured, IDbTransaction transaction)
    {
        var filter = ReadParameters<ReportRequest>(job);
        var effectiveAt = filter.AsOf?.UtcDateTime ?? captured;
        var id = job.Id;
        var risks = ReportSource<RiskDal>(filter.AsOf.HasValue);
        var assessments = ReportSource<RiskAssessmentDal>(filter.AsOf.HasValue);
        var treatments = ReportSource<TreatmentDal>(filter.AsOf.HasValue);
        var links = ReportSource<RiskProcessDal>(filter.AsOf.HasValue);
        var processes = ReportSource<ProcessDal>(filter.AsOf.HasValue);
        var directorates = ReportSource<DirectorateDal>(filter.AsOf.HasValue);
        var hazards = ReportSource<HazardDal>(filter.AsOf.HasValue);
        var categories = ReportSource<RiskCategoryDal>(filter.AsOf.HasValue);
        var assets = ReportSource<AssetDal>(filter.AsOf.HasValue);
        var query = $"""
            WITH risks AS ({risks}), assessments AS ({assessments}), treatments AS ({treatments}),
                 links AS ({links}), processes AS ({processes}), directorates AS ({directorates}),
                 hazards AS ({hazards}), categories AS ({categories}), assets AS ({assets}),
            selected AS (
                SELECT r.* FROM risks r
                WHERE (CAST({EntityMapper.ParameterName(nameof(filter.Status))} AS text) IS NULL OR
                    r.{EntityMapper.ColName<RiskDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(filter.Status))})
                  AND (CAST({EntityMapper.ParameterName(nameof(filter.CategoryId))} AS uuid) IS NULL OR
                    r.{EntityMapper.ColName<RiskDal>(x => x.CategoryId)} = {EntityMapper.ParameterName(nameof(filter.CategoryId))})
                  AND EXISTS (
                    SELECT FROM links l JOIN processes p ON p.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = l.{EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)}
                    WHERE l.{EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)} = r.{EntityMapper.ColName<RiskDal>(x => x.Id)}
                      AND l.{EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL
                      AND (CAST({EntityMapper.ParameterName(nameof(filter.ProcessId))} AS uuid) IS NULL OR
                        p.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(filter.ProcessId))})
                      AND (CAST({EntityMapper.ParameterName(nameof(filter.DirectorateId))} AS uuid) IS NULL OR
                        p.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)} = {EntityMapper.ParameterName(nameof(filter.DirectorateId))}))
            )
            INSERT INTO {EntityMapper.TbName<DocumentRowDal>()} ({EntityMapper.ColName<DocumentRowDal>(x => x.JobId)},
                {EntityMapper.ColName<DocumentRowDal>(x => x.Number)}, {EntityMapper.ColName<DocumentRowDal>(x => x.Data)},
                {EntityMapper.ColName<DocumentRowDal>(x => x.HasError)})
            SELECT {EntityMapper.ParameterName(nameof(id))}, row_number() OVER (ORDER BY r.{EntityMapper.ColName<RiskDal>(x => x.Id)}),
                jsonb_build_object('{nameof(RiskDal)}', to_jsonb(r),
                    '{nameof(HazardDal)}', h.{EntityMapper.ColName<HazardDal>(x => x.Name)},
                    '{nameof(RiskCategoryDal)}', c.{EntityMapper.ColName<RiskCategoryDal>(x => x.Name)},
                    '{nameof(AssetDal)}', asset.{EntityMapper.ColName<AssetDal>(x => x.Name)},
                    '{nameof(ProcessDal)}', COALESCE(org.items, '[]'::jsonb),
                    '{nameof(RiskAssessmentDal)}', COALESCE(a.items, '[]'::jsonb),
                    '{nameof(TreatmentDal)}', COALESCE(m.items, '[]'::jsonb)),
                false
            FROM selected r
            LEFT JOIN hazards h ON h.{EntityMapper.ColName<HazardDal>(x => x.Id)} = r.{EntityMapper.ColName<RiskDal>(x => x.HazardId)}
            LEFT JOIN categories c ON c.{EntityMapper.ColName<RiskCategoryDal>(x => x.Id)} = r.{EntityMapper.ColName<RiskDal>(x => x.CategoryId)}
            LEFT JOIN assets asset ON asset.{EntityMapper.ColName<AssetDal>(x => x.Id)} = r.{EntityMapper.ColName<RiskDal>(x => x.AssetId)}
            LEFT JOIN LATERAL (
                SELECT jsonb_agg(jsonb_build_object('{nameof(ProcessDal)}', p.{EntityMapper.ColName<ProcessDal>(x => x.Name)},
                    '{nameof(DirectorateDal)}', d.{EntityMapper.ColName<DirectorateDal>(x => x.Name)})
                    ORDER BY p.{EntityMapper.ColName<ProcessDal>(x => x.Id)}) AS items
                FROM links l JOIN processes p ON p.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = l.{EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)}
                LEFT JOIN directorates d ON d.{EntityMapper.ColName<DirectorateDal>(x => x.Id)} = p.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)}
                WHERE l.{EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)} = r.{EntityMapper.ColName<RiskDal>(x => x.Id)}
                  AND l.{EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL
            ) org ON true
            LEFT JOIN LATERAL (
                SELECT jsonb_agg(to_jsonb(x) ORDER BY x.{EntityMapper.ColName<RiskAssessmentDal>(x => x.Id)}) AS items
                FROM assessments x WHERE x.{EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskId)} = r.{EntityMapper.ColName<RiskDal>(x => x.Id)}
            ) a ON true
            LEFT JOIN LATERAL (
                SELECT jsonb_agg(to_jsonb(x) ORDER BY x.{EntityMapper.ColName<TreatmentDal>(x => x.Id)}) AS items
                FROM treatments x WHERE x.{EntityMapper.ColName<TreatmentDal>(x => x.RiskId)} = r.{EntityMapper.ColName<RiskDal>(x => x.Id)}
            ) m ON true
            LIMIT {DocumentRules.RowLimit + 1}
            """;
        if (await connection.ExecuteAsync(query, new { id, AsOf = effectiveAt, filter.Status, filter.CategoryId, filter.ProcessId, filter.DirectorateId },
            transaction) > DocumentRules.RowLimit)
        {
            throw new IdentityCommandException(400, DocumentRules.Invalid);
        }
    }

    /// <summary>Преобразует сохранённые camelCase и PascalCase версии в тип исходной таблицы</summary>
    /// <typeparam name="T">Разрешённый источник отчёта</typeparam>
    /// <param name="historical">Использовать исторические версии на заданный момент</param>
    /// <returns>SQL-проекция источника</returns>
    private static string ReportSource<T>(bool historical)
    {
        var table = EntityMapper.TbName<T>();
        if (!historical)
        {
            var currentQuery = $"SELECT * FROM {table}";
            return currentQuery;
        }

        var asOf = nameof(ReportRequest.AsOf);
        var entityId = EntityMapper.ColName<OrgHistoryDal>(x => x.EntityId);
        var recorded = EntityMapper.ColName<OrgHistoryDal>(x => x.RecordedAt);
        var snapshot = EntityMapper.ColName<OrgHistoryDal>(x => x.Snapshot);
        var query = $"""
            SELECT source.* FROM (
                SELECT DISTINCT ON ({entityId}) {snapshot}
                FROM {EntityMapper.TbName<OrgHistoryDal>()}
                WHERE {EntityMapper.ColName<OrgHistoryDal>(x => x.EntityType)} = '{EntityMapper.TbName<T>(false)}'
                  AND {recorded} <= {EntityMapper.ParameterName(asOf)}
                ORDER BY {entityId}, {recorded} DESC, {EntityMapper.ColName<OrgHistoryDal>(x => x.Id)} DESC
            ) v
            CROSS JOIN LATERAL jsonb_populate_record(NULL::{table}, (
                SELECT jsonb_object_agg(columns.attname, pair.value)
                FROM jsonb_each(v.{snapshot}) pair
                JOIN pg_attribute columns ON columns.attrelid = '{table}'::regclass
                    AND columns.attnum > 0 AND NOT columns.attisdropped
                    AND lower(columns.attname) = lower(pair.key)
            )) source
            """;
        return query;
    }
}
