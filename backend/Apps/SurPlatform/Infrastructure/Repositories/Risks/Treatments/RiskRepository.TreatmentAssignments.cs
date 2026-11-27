using System.Data;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.UseCases.Treatments.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Repositories.Risks.Helpers;

namespace Infrastructure.Repositories.Risks;

/// <summary>Проверка и запись текущего исполнителя</summary>
internal sealed partial class RiskRepository
{
    /// <inheritdoc/>
    public Task<TreatmentDal> AssignTreatmentAsync(RiskActor actor, Guid id, bool dispatch, TreatmentActionRequest request)
    {
        return WriteAsync(actor, TreatmentRules.Assign, async transaction =>
        {
            await RequireRightAsync(actor, RiskRules.View, transaction);
            var measure = await ReadAsync<TreatmentDal>(id, EntityMapper.ColName<TreatmentDal>(x => x.Id), transaction);
            RequireVersion(measure.Version, request.Version);
            await RequireTreatmentOwnerAsync(actor, measure, transaction);
            if (dispatch)
            {
                await RequireRightAsync(actor, TreatmentRules.Execute, transaction);
                RequireTreatmentState(measure.Status is TreatmentRules.Draft or TreatmentRules.Approved);
                await RequireTreatmentCycleAsync(measure.RiskId, RiskRules.Active, transaction);
                await SelectTreatmentLinkAsync(measure.RiskId, measure.RiskProcessId, transaction);
                if (measure.Status == TreatmentRules.Draft)
                {
                    var settings = await ReadTreatmentSettingsAsync(transaction);
                    RequireTreatmentState(ApprovalLevel(measure.CostPlanned, settings) is null);
                }
                else
                {
                    var approved = TreatmentRules.Approved;
                    var approvalQuery = $"""
                        SELECT EXISTS (SELECT FROM {EntityMapper.TbName<TreatmentApprovalDal>()}
                            WHERE {EntityMapper.ColName<TreatmentApprovalDal>(x => x.TreatmentId)} = {EntityMapper.ParameterName(nameof(id))}
                            AND {EntityMapper.ColName<TreatmentApprovalDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(approved))})
                        """;
                    RequireTreatmentState(await connection.ExecuteScalarAsync<bool>(approvalQuery, new { id, approved }, transaction));
                }
            }
            else
            {
                RequireTreatmentState(measure.Status is TreatmentRules.Waiting or TreatmentRules.Working or TreatmentRules.Hold);
                await RequireJiraManualControlAsync(measure, transaction);
            }

            var previous = await ReadAssignmentAsync(id, transaction);
            var operationId = Guid.NewGuid();
            if (request.AssigneeId is Guid assigneeId)
            {
                var candidates = await ReadEligibleExecutorsAsync(measure.ProcessId, assigneeId, 0, 1, transaction);
                if (candidates.Length == 0)
                {
                    throw new IdentityCommandException(409, TreatmentRules.InvalidExecutor);
                }
                if (!dispatch && previous?.AssigneeId == assigneeId)
                {
                    return measure;
                }
                var row = new TreatmentAssignmentDal
                {
                    Id = Guid.NewGuid(), TreatmentId = id, AssigneeId = assigneeId,
                    AssignedById = actor.UserId, AssignmentSource = TreatmentRules.Manual
                };
                var query = $"""
                    INSERT INTO {EntityMapper.TbName<TreatmentAssignmentDal>()}
                        ({EntityMapper.ColName<TreatmentAssignmentDal>(x => x.Id)}, {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.TreatmentId)}, {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssigneeId)}, {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignedById)},
                         {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignmentSource)})
                    VALUES ({EntityMapper.ParameterName(nameof(TreatmentAssignmentDal.Id))}, {EntityMapper.ParameterName(nameof(TreatmentAssignmentDal.TreatmentId))}, {EntityMapper.ParameterName(nameof(TreatmentAssignmentDal.AssigneeId))}, {EntityMapper.ParameterName(nameof(TreatmentAssignmentDal.AssignedById))},
                            {EntityMapper.ParameterName(nameof(TreatmentAssignmentDal.AssignmentSource))})
                    ON CONFLICT ({EntityMapper.ColName<TreatmentAssignmentDal>(x => x.TreatmentId)}) DO UPDATE SET
                      {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssigneeId)} = EXCLUDED.{EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssigneeId)},
                      {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignedById)} = EXCLUDED.{EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignedById)},
                      {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignmentSource)} = EXCLUDED.{EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignmentSource)},
                      {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.AssignedAt)} = clock_timestamp() RETURNING *
                    """;
                var assignment = await connection.QuerySingleAsync<TreatmentAssignmentDal>(query, row, transaction);
                await BusinessHistory.RecordAsync(transaction, operationId, actor.UserId, assignment.Id, previous, assignment);
            }
            else
            {
                RequireTreatmentState(!dispatch && measure.Status == TreatmentRules.Hold);
                if (previous is null)
                {
                    return measure;
                }
                var query = $"""
                    DELETE FROM {EntityMapper.TbName<TreatmentAssignmentDal>()} WHERE {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.TreatmentId)} = {EntityMapper.ParameterName(nameof(id))}
                    """;
                await connection.ExecuteAsync(query, new { id }, transaction);
                await BusinessHistory.RecordAsync(transaction, operationId, actor.UserId, previous.Id, previous, null);
            }

            return await SaveTreatmentStatusAsync(measure, dispatch ? TreatmentRules.Waiting : measure.Status,
                actor.UserId, operationId, transaction);
        });
    }

    /// <inheritdoc/>
    public async Task<TreatmentExecutorOption[]> ListTreatmentExecutorsAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        await RequireRightAsync(actor, TreatmentRules.Assign, transaction);
        var measure = await ReadAsync<TreatmentDal>(id, EntityMapper.ColName<TreatmentDal>(x => x.Id), transaction);
        await RequireTreatmentOwnerAsync(actor, measure, transaction);
        var rows = await ReadEligibleExecutorsAsync(measure.ProcessId, null, offset, limit, transaction);
        transaction.Commit();
        return rows;
    }

    /// <summary>Загружает текущее назначение меры</summary>
    /// <param name="id">Мера</param>
    /// <param name="transaction">Транзакция операции</param>
    /// <returns>Назначение, если есть</returns>
    private Task<TreatmentAssignmentDal?> ReadAssignmentAsync(Guid id, IDbTransaction transaction)
    {
        var query = $"SELECT * FROM {EntityMapper.TbName<TreatmentAssignmentDal>()} WHERE {EntityMapper.ColName<TreatmentAssignmentDal>(x => x.TreatmentId)} = {EntityMapper.ParameterName(nameof(id))}";
        return connection.QuerySingleOrDefaultAsync<TreatmentAssignmentDal>(query, new { id }, transaction);
    }

    /// <summary>Проверяет наличие пригодного исполнителя при входе в работу</summary>
    /// <param name="measure">Мера перед запуском или возобновлением</param>
    /// <param name="transaction">Транзакция операции</param>
    private async Task RequireValidAssignmentAsync(TreatmentDal measure, IDbTransaction transaction)
    {
        var assignment = await ReadAssignmentAsync(measure.Id, transaction);
        if (assignment is null ||
            (await ReadEligibleExecutorsAsync(measure.ProcessId, assignment.AssigneeId, 0, 1, transaction)).Length != 1)
        {
            throw new IdentityCommandException(409, TreatmentRules.InvalidExecutor, [measure.Id]);
        }
    }

    /// <summary>Отбирает сотрудников дирекции с действующей ролью исполнителя</summary>
    /// <param name="processId">Процесс меры</param>
    /// <param name="personId">Конкретный кандидат, если выбран</param>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <param name="transaction">Транзакция операции</param>
    /// <returns>Допустимые исполнители</returns>
    private async Task<TreatmentExecutorOption[]> ReadEligibleExecutorsAsync(Guid processId, Guid? personId,
        int offset, int limit, IDbTransaction transaction)
    {
        var roles = new[] { OrganizationRoles.Owner, TreatmentRules.Employee };
        var query = $"""
            SELECT p.{EntityMapper.ColName<OrgPersonDal>(x => x.Id)} AS {EntityMapper.ColName<TreatmentExecutorOption>(x => x.PersonId)},
                u.{EntityMapper.ColName<UserProfileDal>(x => x.FullName)} AS {EntityMapper.ColName<TreatmentExecutorOption>(x => x.FullName)}
            FROM {EntityMapper.TbName<ProcessDal>()} pr JOIN {EntityMapper.TbName<OrgPersonDal>()} p ON p.{EntityMapper.ColName<OrgPersonDal>(x => x.DirectorateId)} = pr.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)}
            JOIN {EntityMapper.TbName<UserProfileDal>()} u ON u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = p.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)}
            WHERE pr.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(processId))} AND p.{EntityMapper.ColName<OrgPersonDal>(x => x.IsActive)} AND u.{EntityMapper.ColName<UserProfileDal>(x => x.IsActive)}
              AND (CAST({EntityMapper.ParameterName(nameof(personId))} AS uuid) IS NULL OR p.{EntityMapper.ColName<OrgPersonDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(personId))})
              AND EXISTS (SELECT FROM {EntityMapper.TbName<UserRoleDal>()} ur JOIN {EntityMapper.TbName<RoleDal>()} r ON r.{EntityMapper.ColName<RoleDal>(x => x.Id)} = ur.{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)}
                  WHERE ur.{EntityMapper.ColName<UserRoleDal>(x => x.UserId)} = p.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)} AND ur.{EntityMapper.ColName<UserRoleDal>(x => x.IsActive)}
                    AND r.{EntityMapper.ColName<RoleDal>(x => x.IsActive)} AND r.{EntityMapper.ColName<RoleDal>(x => x.Code)} = ANY({EntityMapper.ParameterName(nameof(roles))}))
            ORDER BY u.{EntityMapper.ColName<UserProfileDal>(x => x.FullName)}, p.{EntityMapper.ColName<OrgPersonDal>(x => x.Id)} OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var rows = await connection.QueryAsync<TreatmentExecutorOption>(query, new { processId, personId, roles, offset, limit }, transaction);
        return rows.ToArray();
    }
}
