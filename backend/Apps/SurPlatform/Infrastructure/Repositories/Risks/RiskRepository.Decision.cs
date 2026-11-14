using System.Data;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.UseCases.Risks.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Repositories.Risks.Helpers;

namespace Infrastructure.Repositories.Risks;

/// <summary>Однократный выбор способа работы и организационный маршрут принятия</summary>
internal sealed partial class RiskRepository
{
    /// <inheritdoc/>
    public Task<RiskDal> ChooseDecisionAsync(RiskActor actor, Guid id, RiskDecisionRequest request, string mode)
    {
        var right = mode switch
        {
            RiskRules.TreatMode => RiskRules.Edit,
            RiskRules.SplitMode => RiskRules.Split,
            RiskRules.AcceptMode => null,
            _ => throw new IdentityCommandException(400, RiskRules.Invalid)
        };
        return WriteAsync(actor, right, async transaction =>
        {
            var risk = await ReadAsync<RiskDal>(id, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
            if (mode == RiskRules.AcceptMode)
            {
                await RequireAcceptanceContextAsync(actor, risk, transaction);
                await RequireNoBlockingTreatmentsAsync(transaction, id);
            }
            else
            {
                await RequirePrimaryOwnerAsync(actor, id, transaction);
            }

            RequireVersion(risk.Version, request.Version);
            if (risk.Status != RiskRules.Active || risk.AcceptanceLevel is null)
            {
                throw new IdentityCommandException(409, RiskRules.InvalidTransition);
            }
            var current = await ReadCurrentAssessmentAsync(id, transaction)
                ?? throw new IdentityCommandException(409, RiskRules.InvalidTransition);
            if (current.ResponseMode is not null)
            {
                throw new IdentityCommandException(409, RiskRules.ModeSelected);
            }

            var changes = new List<(string Path, object? Before, object After)>();
            if (mode == RiskRules.SplitMode)
            {
                await ReplaceProcessLinksAsync(transaction, id, request, changes);
            }
            else if (request.PrimaryProcessId is not null || request.ProcessIds is not null)
            {
                throw new IdentityCommandException(400, RiskRules.Invalid);
            }

            var assessment = await SetAssessmentStateAsync(transaction, current, true, mode);
            changes.Add((BusinessHistory.AssessmentsPath, current, assessment));
            var status = mode == RiskRules.AcceptMode ? RiskRules.Accepted : risk.Status;
            var updated = await SaveRiskStateAsync(transaction, risk, status, risk.AcceptanceLevel);
            await BusinessHistory.RecordAsync(transaction, Guid.NewGuid(), actor.UserId, id, risk, updated, relatedChanges: changes);
            return updated;
        });
    }

    /// <summary>Проверяет право и актуальное назначение на рассчитанном уровне</summary>
    /// <param name="actor">Исполнитель принятия</param>
    /// <param name="risk">Риск с сохранённым маршрутом</param>
    /// <param name="transaction">Транзакция выбора режима</param>
    private async Task RequireAcceptanceContextAsync(RiskActor actor, RiskDal risk, IDbTransaction transaction)
    {
        if (await HasRightAsync(actor, IdentityRights.AdminCode, transaction))
        {
            return;
        }
        if (risk.AcceptanceLevel == OrganizationRoles.Owner)
        {
            await RequireRightAsync(actor, RiskRules.AcceptOwner, transaction);
            await RequirePrimaryOwnerAsync(actor, risk.Id, transaction);
            return;
        }

        var right = risk.AcceptanceLevel switch
        {
            OrganizationRoles.Director => RiskRules.AcceptDirector,
            OrganizationRoles.Ceo => RiskRules.AcceptCeo,
            _ => throw new IdentityCommandException(403, IdentityErrorCodes.AccessDenied)
        };
        await RequireRightAsync(actor, right, transaction);

        var riskId = risk.Id;
        var userId = actor.UserId;
        var director = OrganizationRoles.Director;
        var ceo = OrganizationRoles.Ceo;
        var specialist = OrganizationRoles.RiskSpecialist;
        var level = risk.AcceptanceLevel;
        var query = $"""
            SELECT EXISTS (
                SELECT FROM {EntityMapper.TbName<ResponsibilityDal>()} a
                JOIN {EntityMapper.TbName<OrgPersonDal>()} person ON person.{EntityMapper.ColName<OrgPersonDal>(x => x.Id)} = a.{EntityMapper.ColName<ResponsibilityDal>(x => x.PersonId)} AND person.{EntityMapper.ColName<OrgPersonDal>(x => x.IsActive)}
                JOIN {EntityMapper.TbName<UserRoleDal>()} assignment ON assignment.{EntityMapper.ColName<UserRoleDal>(x => x.UserId)} = person.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)} AND assignment.{EntityMapper.ColName<UserRoleDal>(x => x.IsActive)}
                JOIN {EntityMapper.TbName<RoleDal>()} role ON role.{EntityMapper.ColName<RoleDal>(x => x.Id)} = assignment.{EntityMapper.ColName<UserRoleDal>(x => x.RoleId)} AND role.{EntityMapper.ColName<RoleDal>(x => x.IsActive)}
                    AND role.{EntityMapper.ColName<RoleDal>(x => x.Code)} = a.{EntityMapper.ColName<ResponsibilityDal>(x => x.ResponsibilityType)}
                JOIN {EntityMapper.TbName<RiskProcessDal>()} link ON link.{EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(riskId))} AND link.{EntityMapper.ColName<RiskProcessDal>(x => x.IsPrimary)} AND link.{EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL
                JOIN {EntityMapper.TbName<ProcessDal>()} process ON process.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = link.{EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)}
                JOIN {EntityMapper.TbName<DirectorateDal>()} directorate ON directorate.{EntityMapper.ColName<DirectorateDal>(x => x.Id)} = process.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)}
                WHERE a.{EntityMapper.ColName<ResponsibilityDal>(x => x.IsActive)} AND person.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)} = {EntityMapper.ParameterName(nameof(userId))}
                  AND (({EntityMapper.ParameterName(nameof(level))} = {EntityMapper.ParameterName(nameof(ceo))} AND a.{EntityMapper.ColName<ResponsibilityDal>(x => x.ResponsibilityType)} = {EntityMapper.ParameterName(nameof(ceo))} AND a.{EntityMapper.ColName<ResponsibilityDal>(x => x.DirectorateId)} IS NULL)
                    OR (process.{EntityMapper.ColName<ProcessDal>(x => x.IsActive)} AND directorate.{EntityMapper.ColName<DirectorateDal>(x => x.IsActive)}
                       AND a.{EntityMapper.ColName<ResponsibilityDal>(x => x.DirectorateId)} = process.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)}
                       AND (({EntityMapper.ParameterName(nameof(level))} = {EntityMapper.ParameterName(nameof(director))} AND a.{EntityMapper.ColName<ResponsibilityDal>(x => x.ResponsibilityType)} = {EntityMapper.ParameterName(nameof(director))})
                         OR ({EntityMapper.ParameterName(nameof(level))} = {EntityMapper.ParameterName(nameof(ceo))} AND a.{EntityMapper.ColName<ResponsibilityDal>(x => x.ResponsibilityType)} = {EntityMapper.ParameterName(nameof(specialist))})))))
            """;
        if (!await connection.ExecuteScalarAsync<bool>(query, new { riskId, userId, director, ceo, specialist, level }, transaction))
        {
            throw new IdentityCommandException(403, IdentityErrorCodes.AccessDenied);
        }
    }

    /// <summary>Применяет полный набор процессов и сохраняет историю исключённых связей</summary>
    /// <param name="transaction">Транзакция выбора режима</param>
    /// <param name="id">Риск</param>
    /// <param name="request">Полный набор и основной процесс</param>
    /// <param name="changes">Изменения связей для общего события риска</param>
    private async Task ReplaceProcessLinksAsync(IDbTransaction transaction, Guid id, RiskDecisionRequest request,
        List<(string Path, object? Before, object After)> changes)
    {
        if (request.ProcessIds is not { Length: >= 2 } processIds ||
            request.PrimaryProcessId is not Guid primary || !processIds.Contains(primary) ||
            processIds.Distinct().Count() != processIds.Length || processIds.Contains(Guid.Empty))
        {
            throw new IdentityCommandException(400, RiskRules.Invalid);
        }

        var processes = await ReadActiveProcessesAsync(processIds, transaction);
        var links = await ReadActiveRiskLinksAsync(id, transaction);
        var selected = processIds.ToHashSet();
        var excluded = links.Where(link => !selected.Contains(link.ProcessId)).Select(link => link.ProcessId).ToArray();
        if (excluded.Length > 0)
        {
            await RequireNoBlockingTreatmentsAsync(transaction, id, excluded);
        }
        foreach (var link in links)
        {
            var keep = selected.Contains(link.ProcessId);
            var isPrimary = keep ? link.ProcessId == primary : link.IsPrimary;
            if (!keep || isPrimary != link.IsPrimary)
            {
                var updated = await SaveProcessLinkAsync(transaction, link, isPrimary, keep);
                changes.Add((BusinessHistory.ProcessesPath, link, updated));
            }
        }

        var existing = links.Select(link => link.ProcessId).ToHashSet();
        foreach (var process in processes)
        {
            if (!existing.Contains(process.Id))
            {
                var link = await InsertProcessLinkAsync(transaction, id, process, process.Id == primary);
                changes.Add((BusinessHistory.ProcessesPath, null, link));
            }
        }
    }

    /// <summary>Проверяет, что процессы и их владельцы действуют</summary>
    /// <param name="processIds">Выбранные процессы</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <returns>Действующие процессы</returns>
    private async Task<ProcessDal[]> ReadActiveProcessesAsync(Guid[] processIds, IDbTransaction transaction)
    {
        var readProcessesQuery = $"""
            SELECT process.* FROM {EntityMapper.TbName<ProcessDal>()} process
            JOIN {EntityMapper.TbName<DirectorateDal>()} directorate ON directorate.{EntityMapper.ColName<DirectorateDal>(x => x.Id)} = process.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)} AND directorate.{EntityMapper.ColName<DirectorateDal>(x => x.IsActive)}
            JOIN {EntityMapper.TbName<OrgPersonDal>()} person ON person.{EntityMapper.ColName<OrgPersonDal>(x => x.Id)} = process.{EntityMapper.ColName<ProcessDal>(x => x.OwnerId)} AND person.{EntityMapper.ColName<OrgPersonDal>(x => x.IsActive)}
            JOIN {EntityMapper.TbName<UserProfileDal>()} profile ON profile.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = person.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)} AND profile.{EntityMapper.ColName<UserProfileDal>(x => x.IsActive)}
            WHERE process.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = ANY({EntityMapper.ParameterName(nameof(processIds))}) AND process.{EntityMapper.ColName<ProcessDal>(x => x.IsActive)}
            """;
        var processes = (await connection.QueryAsync<ProcessDal>(readProcessesQuery, new { processIds }, transaction)).ToArray();
        if (processes.Length != processIds.Length)
        {
            throw new IdentityCommandException(409, RiskRules.InactiveReference);
        }

        return processes;
    }

    /// <summary>Читает действующие связи риска</summary>
    /// <param name="id">Риск</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <returns>Действующие связи</returns>
    private async Task<RiskProcessDal[]> ReadActiveRiskLinksAsync(Guid id, IDbTransaction transaction)
    {
        var readLinksQuery = $"""
            SELECT * FROM {EntityMapper.TbName<RiskProcessDal>()} WHERE {EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(id))} AND {EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL
            ORDER BY {EntityMapper.ColName<RiskProcessDal>(x => x.IsPrimary)} DESC, {EntityMapper.ColName<RiskProcessDal>(x => x.Id)}
            """;
        return (await connection.QueryAsync<RiskProcessDal>(readLinksQuery, new { id }, transaction)).ToArray();
    }

    /// <summary>Создаёт действующую связь с текущим владельцем процесса</summary>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <param name="id">Риск</param>
    /// <param name="process">Действующий процесс</param>
    /// <param name="isPrimary">Признак основной связи</param>
    /// <returns>Созданная связь</returns>
    private async Task<RiskProcessDal> InsertProcessLinkAsync(IDbTransaction transaction, Guid id, ProcessDal process, bool isPrimary)
    {
        var row = new RiskProcessDal { Id = Guid.NewGuid(), RiskId = id, ProcessId = process.Id,
            OwnerId = process.OwnerId, IsPrimary = isPrimary };
        var insertQuery = $"""
            INSERT INTO {EntityMapper.TbName<RiskProcessDal>()}
                ({EntityMapper.ColName<RiskProcessDal>(x => x.Id)}, {EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)}, {EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)}, {EntityMapper.ColName<RiskProcessDal>(x => x.OwnerId)}, {EntityMapper.ColName<RiskProcessDal>(x => x.IsPrimary)})
            VALUES ({EntityMapper.ParameterName(nameof(row.Id))}, {EntityMapper.ParameterName(nameof(row.RiskId))}, {EntityMapper.ParameterName(nameof(row.ProcessId))}, {EntityMapper.ParameterName(nameof(row.OwnerId))}, {EntityMapper.ParameterName(nameof(row.IsPrimary))}) RETURNING *
            """;
        return await connection.QuerySingleAsync<RiskProcessDal>(insertQuery, row, transaction);
    }

    /// <summary>Меняет основную связь или закрывает исключённую связь</summary>
    /// <param name="transaction">Транзакция разделения</param>
    /// <param name="link">Ожидаемая действующая связь</param>
    /// <param name="primary">Признак основного процесса</param>
    /// <param name="keep">Сохраняется ли связь в новом наборе</param>
    /// <returns>Новая версия связи</returns>
    private async Task<RiskProcessDal> SaveProcessLinkAsync(IDbTransaction transaction, RiskProcessDal link, bool primary, bool keep)
    {
        var query = $"""
            UPDATE {EntityMapper.TbName<RiskProcessDal>()} SET {EntityMapper.ColName<RiskProcessDal>(x => x.IsPrimary)} = {EntityMapper.ParameterName(nameof(primary))}, {EntityMapper.ColName<RiskProcessDal>(x => x.Version)} = gen_random_uuid(),
                {EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} = CASE WHEN {EntityMapper.ParameterName(nameof(keep))} THEN NULL ELSE clock_timestamp() END
            WHERE {EntityMapper.ColName<RiskProcessDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(link.Id))} AND {EntityMapper.ColName<RiskProcessDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(link.Version))} AND {EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL RETURNING *
            """;
        return await connection.QuerySingleOrDefaultAsync<RiskProcessDal>(query, new { link.Id, link.Version, primary, keep }, transaction)
            ?? throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
    }
}
