using Infrastructure.Repositories.Risks.Models;
using Application.Models;
using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;

namespace Infrastructure.Repositories.Risks.Helpers;

/// <summary>Записывает обязательное поручение журнала внутри бизнес-транзакции</summary>
internal static class BusinessHistory
{
    /// <summary>Источник интерактивной команды</summary>
    private const string UserSource = "USER";

    /// <summary>Источник автоматического последствия команды</summary>
    private const string SystemSource = "SYSTEM";

    /// <summary>Путь связей в событии риска</summary>
    internal const string ProcessesPath = "risk_processes";

    /// <summary>Путь оценок в событии риска</summary>
    internal const string AssessmentsPath = "Assessment";

    /// <summary>Операция создания</summary>
    private const string CreateOperation = "CREATE";

    /// <summary>Операция изменения</summary>
    private const string UpdateOperation = "UPDATE";

    /// <summary>Операция логического удаления</summary>
    private const string DeleteOperation = "SOFT_DELETE";

    /// <summary>Добавляет событие только при изменении бизнес-полей</summary>
    /// <typeparam name="T">Тип записи без чувствительных данных</typeparam>
    /// <param name="transaction">Текущая транзакция</param>
    /// <param name="operationId">Общий идентификатор операции</param>
    /// <param name="actorUserId">Инициатор</param>
    /// <param name="entityId">Идентификатор записи</param>
    /// <param name="before">Прежняя запись</param>
    /// <param name="after">Новая запись</param>
    /// <param name="entityTypeOverride">Тип таблицы для универсальной проекции</param>
    /// <param name="relatedPath">Путь изменившейся дочерней записи в агрегате</param>
    /// <param name="relatedBefore">Прежнее состояние дочерней записи</param>
    /// <param name="relatedAfter">Новое состояние дочерней записи</param>
    /// <param name="system">Автоматическое последствие действия пользователя</param>
    /// <param name="relatedChanges">Другие изменившиеся записи того же риска</param>
    internal static async Task RecordAsync<T>(IDbTransaction transaction, Guid operationId, Guid actorUserId,
        Guid entityId, T? before, T after, string? entityTypeOverride = null,
        string? relatedPath = null, object? relatedBefore = null, object? relatedAfter = null, bool system = false, IReadOnlyList<(string Path, object? Before, object After)>? relatedChanges = null) where T : class
    {
        var previous = JsonSerializer.SerializeToNode(before, JsonSerializerOptions.Web) as JsonObject;
        var next = JsonSerializer.SerializeToNode(after, JsonSerializerOptions.Web) as JsonObject ?? new JsonObject();
        var changes = Compare(previous, next).ToList();
        if (relatedPath is not null && relatedAfter is not null)
        {
            await AddRelatedAsync(transaction, actorUserId, changes, relatedPath, relatedBefore, relatedAfter);
        }
        if (relatedChanges is not null)
        {
            foreach (var related in relatedChanges)
            {
                await AddRelatedAsync(transaction, actorUserId, changes, related.Path, related.Before, related.After);
            }
        }

        if (changes.Count == 0)
        {
            return;
        }

        if (after is RiskDal or RiskAssessmentDal or RiskReference)
        {
            await RecordSourceVersionAsync(transaction, actorUserId, entityId,
                entityTypeOverride ?? EntityMapper.TbName<T>(false), after);
        }
        var deleted = after is RiskCommentDal { IsDeleted: true } && before is RiskCommentDal { IsDeleted: false };
        var eventId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;
        var entityType = entityTypeOverride ?? EntityMapper.TbName<T>(false);
        var payload = JsonSerializer.Serialize(new
        {
            eventId, operationId, entityType, entityId, occurredAt,
            actorUserId = system ? Application.Constants.IdentityAccounts.SystemUserId : actorUserId,
            initiatorUserId = system ? (Guid?)actorUserId : null,
            responsibleUserId = system ? (Guid?)actorUserId : null,
            source = system ? SystemSource : UserSource, operation = before is null ? CreateOperation : deleted ? DeleteOperation : UpdateOperation,
            changes, commentTextBeforeDeletion = deleted && before is RiskCommentDal comment ? comment.Text : null
        }, JsonSerializerOptions.Web);
        var query = $"""
            INSERT INTO {EntityMapper.TbName<BusinessChangeJobDal>()}
                ({EntityMapper.ColName<BusinessChangeJobDal>(x => x.EventId)},
                 {EntityMapper.ColName<BusinessChangeJobDal>(x => x.OccurredAt)},
                 {EntityMapper.ColName<BusinessChangeJobDal>(x => x.Payload)})
            VALUES ({EntityMapper.ParameterName(nameof(eventId))}, {EntityMapper.ParameterName(nameof(occurredAt))},
                CAST({EntityMapper.ParameterName(nameof(payload))} AS jsonb))
            """;
        var connection = transaction.Connection ?? throw new InvalidOperationException();
        await connection.ExecuteAsync(query, new { eventId, occurredAt, payload }, transaction);
    }

    /// <summary>Добавляет поля дочерней записи и сохраняет её отчётную версию</summary>
    /// <param name="transaction">Транзакция риска</param>
    /// <param name="actor">Инициатор</param>
    /// <param name="changes">Список полей события риска</param>
    /// <param name="path">Раздел пути</param>
    /// <param name="before">Прежняя запись</param>
    /// <param name="after">Новая запись</param>
    private static async Task AddRelatedAsync(IDbTransaction transaction, Guid actor, List<BusinessFieldChange> changes,
        string path, object? before, object after)
    {
        var (id, entityType) = after switch
        {
            RiskProcessDal process => (process.Id, EntityMapper.TbName<RiskProcessDal>(false)),
            RiskAssessmentDal assessment => (assessment.Id, EntityMapper.TbName<RiskAssessmentDal>(false)),
            _ => throw new InvalidOperationException()
        };
        var previous = JsonSerializer.SerializeToNode(before, JsonSerializerOptions.Web) as JsonObject;
        var next = JsonSerializer.SerializeToNode(after, JsonSerializerOptions.Web) as JsonObject ?? new JsonObject();
        var relatedFields = Compare(previous, next, $"{path}[{id}].").ToArray();
        if (relatedFields.Length > 0)
        {
            changes.AddRange(relatedFields);
            await RecordSourceVersionAsync(transaction, actor, id, entityType, after);
        }
    }

    /// <summary>Возвращает только фактически изменённые поля с их полными путями</summary>
    /// <param name="previous">Прежние значения полей</param>
    /// <param name="next">Новые значения полей</param>
    /// <param name="prefix">Путь дочерней записи риска</param>
    /// <returns>Изменения с прежними и новыми значениями</returns>
    private static IEnumerable<BusinessFieldChange> Compare(JsonObject? previous, JsonObject next, string? prefix = null)
    {
        var version = JsonNamingPolicy.CamelCase.ConvertName(nameof(RiskDal.Version));
        var updatedAt = JsonNamingPolicy.CamelCase.ConvertName(nameof(RiskDal.UpdatedAt));
        foreach (var field in next)
        {
            var oldValue = previous is null ? null : previous[field.Key];
            if (field.Key != version && field.Key != updatedAt && !JsonNode.DeepEquals(oldValue, field.Value))
            {
                yield return new BusinessFieldChange(prefix + field.Key, oldValue, field.Value);
            }
        }
    }

    /// <summary>Сохраняет полную версию отчётного источника с моментом её фиксации</summary>
    /// <typeparam name="T">Модель отчётного источника</typeparam>
    /// <param name="transaction">Транзакция бизнес-команды</param>
    /// <param name="actorId">Инициатор изменения</param>
    /// <param name="entityId">Идентификатор источника</param>
    /// <param name="entityType">Тип источника</param>
    /// <param name="source">Новое состояние источника</param>
    private static async Task RecordSourceVersionAsync<T>(IDbTransaction transaction, Guid actorId,
        Guid entityId, string entityType, T source) where T : class
    {
        var id = source switch
        {
            RiskDal risk => risk.Version,
            RiskProcessDal process => process.Version,
            RiskAssessmentDal assessment => assessment.Version,
            RiskReference reference => reference.Version,
            _ => throw new InvalidOperationException()
        };
        var snapshot = JsonSerializer.Serialize(source, JsonSerializerOptions.Web);
        var query = $"""
            INSERT INTO {EntityMapper.TbName<OrgHistoryDal>()}
                ({EntityMapper.ColName<OrgHistoryDal>(x => x.Id)}, {EntityMapper.ColName<OrgHistoryDal>(x => x.EntityId)},
                 {EntityMapper.ColName<OrgHistoryDal>(x => x.EntityType)}, {EntityMapper.ColName<OrgHistoryDal>(x => x.ActorId)},
                 {EntityMapper.ColName<OrgHistoryDal>(x => x.Snapshot)})
            VALUES ({EntityMapper.ParameterName(nameof(id))}, {EntityMapper.ParameterName(nameof(entityId))},
                {EntityMapper.ParameterName(nameof(entityType))}, {EntityMapper.ParameterName(nameof(actorId))},
                CAST({EntityMapper.ParameterName(nameof(snapshot))} AS jsonb))
            """;
        var connection = transaction.Connection ?? throw new InvalidOperationException();
        await connection.ExecuteAsync(query, new { id, entityId, entityType, actorId, snapshot }, transaction);
    }
}
