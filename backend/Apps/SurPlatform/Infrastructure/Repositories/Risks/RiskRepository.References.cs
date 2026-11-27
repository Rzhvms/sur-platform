using Infrastructure.Repositories.Risks.Helpers;
using System.Data;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.UseCases.Risks.Dto;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Npgsql;

namespace Infrastructure.Repositories.Risks;

/// <summary>Администрирование справочников и варианты выбора для паспорта</summary>
internal sealed partial class RiskRepository
{
    /// <inheritdoc/>
    public async Task<RiskReference[]> ListReferencesAsync(RiskActor actor, RiskReferenceKind kind, bool available,
        int offset, int limit, string? search)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        if (available)
        {
            if (!await HasRightAsync(actor, RiskRules.Create, transaction) &&
                !await HasRightAsync(actor, RiskRules.Edit, transaction) &&
                !await HasRightAsync(actor, RiskRules.View, transaction))
            {
                throw new IdentityCommandException(403, IdentityErrorCodes.AccessDenied);
            }
        }
        else
        {
            await RequireRightAsync(actor, IdentityRights.AdminCode, transaction);
        }

        var table = ReferenceTable(kind);
        var group = kind == RiskReferenceKind.Hazards;
        var groupProjection = group ? $"r.{EntityMapper.ColName<HazardDal>(x => x.GroupId)}" : NullGroup;
        var groupAvailability = group && available
            ? $"""
                AND EXISTS (SELECT FROM {EntityMapper.TbName<HazardGroupDal>()} g
                    WHERE g.{EntityMapper.ColName<HazardGroupDal>(x => x.Id)} = r.{EntityMapper.ColName<HazardDal>(x => x.GroupId)} AND g.{EntityMapper.ColName<HazardGroupDal>(x => x.IsActive)})
                """
            : string.Empty;
        var term = search?.Trim() ?? string.Empty;
        var query = $"""
            SELECT r.*, {groupProjection} AS {EntityMapper.ColName<RiskReference>(x => x.GroupId)}
            FROM {table} r
            WHERE (NOT {EntityMapper.ParameterName(nameof(available))} OR r.{EntityMapper.ColName<HazardGroupDal>(x => x.IsActive)})
              AND strpos(lower(r.{EntityMapper.ColName<HazardGroupDal>(x => x.Name)}), lower({EntityMapper.ParameterName(nameof(term))})) > 0
              {groupAvailability}
            ORDER BY r.{EntityMapper.ColName<HazardGroupDal>(x => x.Name)}, r.{EntityMapper.ColName<HazardGroupDal>(x => x.Id)} OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var rows = await connection.QueryAsync<RiskReference>(query, new { available, term, offset, limit }, transaction);
        transaction.Commit();
        return rows.ToArray();
    }

    /// <summary>Тип пустой группы в универсальной проекции справочников</summary>
    private const string NullGroup = "NULL::uuid";

    /// <inheritdoc/>
    public async Task<RiskReference> SaveReferenceAsync(RiskActor actor, RiskReferenceKind kind, Guid? id, RiskReferenceRequest request)
    {
        try
        {
            return await WriteAsync(actor, IdentityRights.AdminCode, async transaction =>
            {
                var table = ReferenceTable(kind);
                var groupId = request.GroupId;
                var isHazard = kind == RiskReferenceKind.Hazards;
                if (isHazard != groupId.HasValue || (id is null && (request.Version is not null || request.IsActive == false)))
                {
                    throw new IdentityCommandException(400, RiskRules.Invalid);
                }

                var groupProjection = isHazard ? $"{EntityMapper.ColName<HazardDal>(x => x.GroupId)}" : NullGroup;
                var entryId = id ?? Guid.NewGuid();
                var readQuery = $"""
                    SELECT *, {groupProjection} AS {EntityMapper.ColName<RiskReference>(x => x.GroupId)}
                    FROM {table} WHERE {EntityMapper.ColName<HazardGroupDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(entryId))}
                    """;
                RiskReference? previous = null;
                if (id is not null)
                {
                    previous = await connection.QuerySingleOrDefaultAsync<RiskReference>(readQuery, new { entryId }, transaction)
                        ?? throw new IdentityCommandException(404, RiskRules.NotFound);
                    RequireVersion(previous.Version, request.Version);
                }

                var active = request.IsActive ?? previous?.IsActive ?? true;
                if (groupId is Guid requiredGroup && (previous?.GroupId != groupId || (active && previous?.IsActive != true)))
                {
                    var group = await ReadAsync<HazardGroupDal>(requiredGroup, EntityMapper.ColName<HazardGroupDal>(x => x.Id), transaction);
                    if (!group.IsActive)
                    {
                        throw new IdentityCommandException(409, RiskRules.InactiveReference);
                    }
                }

                var name = RequiredText(request.Name);
                var description = request.Description?.Trim();
                if (previous is not null && previous.Name == name && previous.Description == description
                    && previous.IsActive == active && previous.GroupId == groupId)
                {
                    return previous;
                }

                var version = request.Version;
                var groupColumn = isHazard ? $", {EntityMapper.ColName<HazardDal>(x => x.GroupId)}" : string.Empty;
                var groupParameter = isHazard ? $", {EntityMapper.ParameterName(nameof(groupId))}" : string.Empty;
                var groupAssignment = isHazard ? $", {EntityMapper.ColName<HazardDal>(x => x.GroupId)} = {EntityMapper.ParameterName(nameof(groupId))}" : string.Empty;
                var insertQuery = $"""
                    INSERT INTO {table} ({EntityMapper.ColName<HazardGroupDal>(x => x.Id)}, {EntityMapper.ColName<HazardGroupDal>(x => x.Name)}, {EntityMapper.ColName<HazardGroupDal>(x => x.Description)}, {EntityMapper.ColName<HazardGroupDal>(x => x.IsActive)}{groupColumn})
                    VALUES ({EntityMapper.ParameterName(nameof(entryId))}, {EntityMapper.ParameterName(nameof(name))}, {EntityMapper.ParameterName(nameof(description))}, {EntityMapper.ParameterName(nameof(active))}{groupParameter})
                    """;
                var updateQuery = $"""
                    UPDATE {table} SET {EntityMapper.ColName<HazardGroupDal>(x => x.Name)} = {EntityMapper.ParameterName(nameof(name))}, {EntityMapper.ColName<HazardGroupDal>(x => x.Description)} = {EntityMapper.ParameterName(nameof(description))},
                        {EntityMapper.ColName<HazardGroupDal>(x => x.IsActive)} = {EntityMapper.ParameterName(nameof(active))}, {EntityMapper.ColName<HazardGroupDal>(x => x.Version)} = gen_random_uuid(){groupAssignment}
                    WHERE {EntityMapper.ColName<HazardGroupDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(entryId))} AND {EntityMapper.ColName<HazardGroupDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(version))}
                    """;
                var updatedCount = await connection.ExecuteAsync(id is null ? insertQuery : updateQuery,
                    new { entryId, name, description, active, groupId, version }, transaction);
                if (updatedCount != 1)
                {
                    throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
                }

                var current = await connection.QuerySingleAsync<RiskReference>(readQuery, new { entryId }, transaction);
                var operationId = Guid.NewGuid();
                if (kind == RiskReferenceKind.HazardGroups)
                {
                    await RecordReferenceAsync<HazardGroupDal>(transaction, operationId, actor.UserId, entryId, previous, current);
                }
                else if (kind == RiskReferenceKind.Hazards)
                {
                    await RecordReferenceAsync<HazardDal>(transaction, operationId, actor.UserId, entryId, previous, current);
                }
                else
                {
                    await RecordReferenceAsync<RiskCategoryDal>(transaction, operationId, actor.UserId, entryId, previous, current);
                }

                return current;
            });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new IdentityCommandException(409, RiskRules.Duplicate);
        }
    }

    /// <summary>Пишет историю справочника с типом исходной таблицы</summary>
    /// <param name="transaction">Транзакция команды</param>
    /// <param name="operationId">Идентификатор операции</param>
    /// <param name="actor">Администратор</param>
    /// <param name="id">Идентификатор записи</param>
    /// <param name="before">Предыдущее состояние</param>
    /// <param name="after">Текущее состояние</param>
    private static Task RecordReferenceAsync<T>(IDbTransaction transaction, Guid operationId, Guid actor, Guid id,
        RiskReference? before, RiskReference after)
    {
        return BusinessHistory.RecordAsync(transaction, operationId, actor, id, before, after, EntityMapper.TbName<T>(false));
    }

    /// <summary>Разрешает имя таблицы из закрытого перечня справочников</summary>
    /// <param name="kind">Вид справочника</param>
    /// <returns>Доверенный идентификатор таблицы</returns>
    private static string ReferenceTable(RiskReferenceKind kind)
    {
        return kind switch
        {
            RiskReferenceKind.HazardGroups => EntityMapper.TbName<HazardGroupDal>(),
            RiskReferenceKind.Hazards => EntityMapper.TbName<HazardDal>(),
            RiskReferenceKind.Categories => EntityMapper.TbName<RiskCategoryDal>(),
            _ => throw new IdentityCommandException(400, RiskRules.Invalid)
        };
    }
}
