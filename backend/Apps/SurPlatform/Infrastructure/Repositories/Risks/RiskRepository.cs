using Infrastructure.Repositories.History.Helpers;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.Ports.Repositories;
using Application.UseCases.Risks.Dto.Request;
using Application.UseCases.Risks.Dto;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Observability;
using Infrastructure.Repositories.Assessments.Interfaces;
using Infrastructure.Repositories.Jira.Interfaces;
using Infrastructure.Repositories.Reviews.Interfaces;
using Infrastructure.Repositories.Risks.Interfaces;
using Infrastructure.Repositories.Risks.Models;
using Infrastructure.Repositories.Sessions.Interfaces;
using Infrastructure.Repositories.Shared;
using Infrastructure.Repositories.Treatments.Interfaces;
using System.Data;
using System.Text.Json;
using Domain.DalModels.Assessments;
using Domain.DalModels.Organization;
using Domain.DalModels.Risks;
using Domain.DalModels.Roles;
using Domain.DalModels.Users;

namespace Infrastructure.Repositories.Risks;

/// <summary>Реестр рисков и операции с паспортом</summary>
internal sealed class RiskRepository : ClientRepository, IRiskRepository, IRiskOrganizationRepository
{
    /// <summary>Поручения Jira и режим управления мерой</summary>
    private readonly IJiraStateRepository _jiraStateRepository;

    /// <summary>Запуск и завершение пересмотра в общей транзакции</summary>
    private readonly IReviewStateRepository _reviewStateRepository;

    /// <summary>Текущая оценка и версии состояния риска</summary>
    private readonly IRiskAssessmentStateRepository _riskAssessmentStateRepository;

    /// <summary>Паспорт, связи процессов и технический пересчет риска</summary>
    private readonly IRiskStateRepository _riskStateRepository;

    /// <summary>Назначения и переходы состояния мер</summary>
    private readonly ITreatmentStateRepository _treatmentStateRepository;

    /// <summary>Инициализирует операции выбранной клиентской базы</summary>
    /// <param name="connection">Подключение выбранной клиентской базы</param>
    /// <param name="sessions">Проверка сессии и эффективных прав в транзакции</param>
    /// <param name="operations">Метрики операций базы</param>
    /// <param name="jiraStateRepository">Поручения Jira и режим управления мерой</param>
    /// <param name="reviewStateRepository">Запуск и завершение пересмотра в общей транзакции</param>
    /// <param name="riskAssessmentStateRepository">Текущая оценка и версии состояния риска</param>
    /// <param name="riskStateRepository">Паспорт, связи процессов и технический пересчет риска</param>
    /// <param name="treatmentStateRepository">Назначения и переходы состояния мер</param>
    public RiskRepository(
        IDbConnection connection,
        ITransactionalSessionRepository sessions,
        OperationsMetrics operations,
        IJiraStateRepository jiraStateRepository,
        IReviewStateRepository reviewStateRepository,
        IRiskAssessmentStateRepository riskAssessmentStateRepository,
        IRiskStateRepository riskStateRepository,
        ITreatmentStateRepository treatmentStateRepository)
        : base(connection, sessions, operations)
    {
        _jiraStateRepository = jiraStateRepository;
        _reviewStateRepository = reviewStateRepository;
        _riskAssessmentStateRepository = riskAssessmentStateRepository;
        _riskStateRepository = riskStateRepository;
        _treatmentStateRepository = treatmentStateRepository;
    }

    /// <summary>Системный комментарий при закрытии</summary>
    private const string ClosedComment = "Риск закрыт. Причина: ";

    /// <summary>Системный комментарий при возврате закрытого риска</summary>
    private const string ReopenedComment = "Закрытый риск возвращён на переоценку";

    /// <inheritdoc/>
    public async Task<RiskCommentDal[]> ListCommentsAsync(RiskActor actor, Guid riskId, int offset, int limit)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        await ReadAsync<RiskDal>(riskId, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<RiskCommentDal>()} WHERE {EntityMapper.ColName<RiskCommentDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(riskId))} AND NOT {EntityMapper.ColName<RiskCommentDal>(x => x.IsDeleted)}
            ORDER BY {EntityMapper.ColName<RiskCommentDal>(x => x.CreatedAt)}, {EntityMapper.ColName<RiskCommentDal>(x => x.Id)} OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var rows = await _connection.QueryAsync<RiskCommentDal>(query, new { riskId, offset, limit }, transaction);
        transaction.Commit();
        return rows.ToArray();
    }

    /// <inheritdoc/>
    public Task<RiskCommentDal> SaveCommentAsync(RiskActor actor, Guid riskId, Guid? id, RiskCommentRequest request)
    {
        return ChangeCommentAsync(actor, riskId, id, request.Version, RequiredText(request.Text), false);
    }

    /// <inheritdoc/>
    public async Task DeleteCommentAsync(RiskActor actor, Guid riskId, Guid id, Guid version)
    {
        await ChangeCommentAsync(actor, riskId, id, version, null, true);
    }

    /// <summary>Проверяет доступ, автора, системность и ожидаемую версию комментария</summary>
    /// <param name="actor">Инициатор</param>
    /// <param name="riskId">Родительский риск</param>
    /// <param name="id">Редактируемый комментарий</param>
    /// <param name="version">Версия комментария</param>
    /// <param name="text">Новый текст</param>
    /// <param name="delete">Логическое удаление</param>
    /// <returns>Состояние комментария после команды</returns>
    private Task<RiskCommentDal> ChangeCommentAsync(RiskActor actor, Guid riskId, Guid? id, Guid? version, string? text, bool delete)
    {
        return WriteAsync(actor, RiskRules.Comment, async transaction =>
        {
            await RequireRightAsync(actor, RiskRules.View, transaction);
            await ReadAsync<RiskDal>(riskId, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
            var operationId = Guid.NewGuid();
            if (id is null)
            {
                return await InsertCommentAsync(transaction, operationId, riskId, actor.UserId, RequiredText(text), false);
            }

            var previous = await ReadAsync<RiskCommentDal>(id.Value, EntityMapper.ColName<RiskCommentDal>(x => x.Id), transaction);
            if (previous.RiskId != riskId || previous.IsDeleted)
            {
                throw new IdentityCommandException(404, RiskRules.NotFound);
            }

            if (previous.AuthorId != actor.UserId && !await HasRightAsync(actor, IdentityRights.AdminCode, transaction))
            {
                throw new IdentityCommandException(403, IdentityErrorCodes.AccessDenied);
            }

            if (previous.IsSystem)
            {
                throw new IdentityCommandException(409, RiskRules.CommentLocked);
            }

            RequireVersion(previous.Version, version);
            if (!delete && previous.Text == text)
            {
                return previous;
            }

            var actorId = actor.UserId;
            var deleteQuery = $"""
                UPDATE {EntityMapper.TbName<RiskCommentDal>()} SET {EntityMapper.ColName<RiskCommentDal>(x => x.IsDeleted)} = true, {EntityMapper.ColName<RiskCommentDal>(x => x.DeletedAt)} = clock_timestamp(),
                    {EntityMapper.ColName<RiskCommentDal>(x => x.DeletedById)} = {EntityMapper.ParameterName(nameof(actorId))}, {EntityMapper.ColName<RiskCommentDal>(x => x.Version)} = gen_random_uuid()
                WHERE {EntityMapper.ColName<RiskCommentDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))} AND {EntityMapper.ColName<RiskCommentDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(version))} RETURNING *
                """;
            var editQuery = $"""
                UPDATE {EntityMapper.TbName<RiskCommentDal>()} SET {EntityMapper.ColName<RiskCommentDal>(x => x.Text)} = {EntityMapper.ParameterName(nameof(text))}, {EntityMapper.ColName<RiskCommentDal>(x => x.UpdatedAt)} = clock_timestamp(),
                    {EntityMapper.ColName<RiskCommentDal>(x => x.Version)} = gen_random_uuid()
                WHERE {EntityMapper.ColName<RiskCommentDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))} AND {EntityMapper.ColName<RiskCommentDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(version))} RETURNING *
                """;
            var updated = await _connection.QuerySingleOrDefaultAsync<RiskCommentDal>(delete ? deleteQuery : editQuery,
                new { id, version, actorId, text }, transaction)
                ?? throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
            await BusinessHistory.RecordAsync(transaction, operationId, actorId, updated.Id, previous, updated);
            return updated;
        });
    }

    /// <summary>Создаёт комментарий и поручение в транзакции профильной операции</summary>
    /// <param name="transaction">Транзакция команды</param>
    /// <param name="operationId">Общий идентификатор операции</param>
    /// <param name="riskId">Родительский риск</param>
    /// <param name="authorId">Инициатор</param>
    /// <param name="text">Текст</param>
    /// <param name="system">Признак системного сообщения</param>
    /// <returns>Записанный комментарий</returns>
    private static async Task<RiskCommentDal> InsertCommentAsync(IDbTransaction transaction, Guid operationId,
        Guid riskId, Guid authorId, string text, bool system)
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<RiskCommentDal>()}
                ({EntityMapper.ColName<RiskCommentDal>(x => x.Id)}, {EntityMapper.ColName<RiskCommentDal>(x => x.RiskId)}, {EntityMapper.ColName<RiskCommentDal>(x => x.AuthorId)}, {EntityMapper.ColName<RiskCommentDal>(x => x.Text)},
                 {EntityMapper.ColName<RiskCommentDal>(x => x.IsSystem)}, {EntityMapper.ColName<RiskCommentDal>(x => x.IsDeleted)}, {EntityMapper.ColName<RiskCommentDal>(x => x.CreatedAt)}, {EntityMapper.ColName<RiskCommentDal>(x => x.UpdatedAt)})
            VALUES (gen_random_uuid(), {EntityMapper.ParameterName(nameof(riskId))}, {EntityMapper.ParameterName(nameof(authorId))}, {EntityMapper.ParameterName(nameof(text))}, {EntityMapper.ParameterName(nameof(system))}, false, statement_timestamp(), statement_timestamp())
            RETURNING *
            """;
        var db = transaction.Connection ?? throw new InvalidOperationException();
        var comment = await db.QuerySingleAsync<RiskCommentDal>(query, new { riskId, authorId, text, system }, transaction);
        await BusinessHistory.RecordAsync(transaction, operationId, authorId, comment.Id, null, comment, system: system);
        return comment;
    }

    /// <inheritdoc/>
    public Task<RiskDal> TransitionAsync(RiskActor actor, Guid id, RiskTransitionRequest request, bool close)
    {
        return WriteAsync(actor, close ? RiskRules.Close : RiskRules.Edit, async transaction =>
        {
            var previous = await ReadAsync<RiskDal>(id, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
            await _riskStateRepository.RequirePrimaryOwnerAsync(actor, id, transaction);
            var version = request.Version;
            RequireVersion(previous.Version, version);
            var valid = close
                ? previous.Status is RiskRules.Draft or RiskRules.Active or RiskRules.AwaitingReview or RiskRules.Accepted
                : previous.Status == RiskRules.Accepted || previous.Status == RiskRules.Closed && request.Confirmed;
            if (!valid)
            {
                throw new IdentityCommandException(409, RiskRules.InvalidTransition);
            }

            var operationId = Guid.NewGuid();
            if (!close)
            {
                var reopened = await _reviewStateRepository.StartSingleReviewAsync(previous, ReviewRules.Manual, ReviewRules.RiskCard, id,
                    actor.UserId, operationId, transaction);
                if (previous.Status == RiskRules.Closed)
                {
                    await InsertCommentAsync(transaction, operationId, id, actor.UserId, ReopenedComment, true);
                }
                return reopened;
            }

            await _treatmentStateRepository.RequireNoBlockingTreatmentsAsync(transaction, id);
            var reason = RequiredText(request.Reason);
            var status = RiskRules.Closed;
            if (previous.Status == RiskRules.AwaitingReview)
            {
                await _reviewStateRepository.CompleteReviewAsync(id, actor.UserId, transaction, null, reason);
            }

            var query = $"""
                UPDATE {EntityMapper.TbName<RiskDal>()} SET {EntityMapper.ColName<RiskDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(status))}, {EntityMapper.ColName<RiskDal>(x => x.CloseReason)} = {EntityMapper.ParameterName(nameof(reason))},
                    {EntityMapper.ColName<RiskDal>(x => x.Version)} = gen_random_uuid(), {EntityMapper.ColName<RiskDal>(x => x.UpdatedAt)} = clock_timestamp()
                WHERE {EntityMapper.ColName<RiskDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))} AND {EntityMapper.ColName<RiskDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(version))} RETURNING *
                """;
            var updated = await _connection.QuerySingleAsync<RiskDal>(query, new { id, version, status, reason }, transaction);
            await BusinessHistory.RecordAsync(transaction, operationId, actor.UserId, id, previous, updated);
            await InsertCommentAsync(transaction, operationId, id, actor.UserId, ClosedComment + reason, true);
            return updated;
        });
    }

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
                await _treatmentStateRepository.RequireNoBlockingTreatmentsAsync(transaction, id);
            }
            else
            {
                await _riskStateRepository.RequirePrimaryOwnerAsync(actor, id, transaction);
            }

            RequireVersion(risk.Version, request.Version);
            if (risk.Status != RiskRules.Active || risk.AcceptanceLevel is null)
            {
                throw new IdentityCommandException(409, RiskRules.InvalidTransition);
            }
            var current = await _riskAssessmentStateRepository.ReadCurrentAssessmentAsync(id, transaction)
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

            var assessment = await _riskAssessmentStateRepository.SetAssessmentStateAsync(transaction, current, true, mode);
            changes.Add((BusinessHistory.AssessmentsPath, current, assessment));
            var status = mode == RiskRules.AcceptMode ? RiskRules.Accepted : risk.Status;
            var updated = await _riskAssessmentStateRepository.SaveRiskStateAsync(transaction, risk, status, risk.AcceptanceLevel);
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
            await _riskStateRepository.RequirePrimaryOwnerAsync(actor, risk.Id, transaction);
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
        if (!await _connection.ExecuteScalarAsync<bool>(query, new { riskId, userId, director, ceo, specialist, level }, transaction))
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

        var processes = await _riskStateRepository.ReadActiveProcessesAsync(processIds, transaction);
        var links = await _riskStateRepository.ReadActiveRiskLinksAsync(id, transaction);
        var selected = processIds.ToHashSet();
        var excluded = links.Where(link => !selected.Contains(link.ProcessId)).Select(link => link.ProcessId).ToArray();
        if (excluded.Length > 0)
        {
            await _treatmentStateRepository.RequireNoBlockingTreatmentsAsync(transaction, id, excluded);
        }
        foreach (var link in links)
        {
            var keep = selected.Contains(link.ProcessId);
            var isPrimary = keep && link.ProcessId == primary;
            if (!keep || isPrimary != link.IsPrimary)
            {
                var updated = await _riskStateRepository.SaveProcessLinkAsync(transaction, link, isPrimary, keep);
                changes.Add((BusinessHistory.ProcessesPath, link, updated));
            }
        }

        var existing = links.Select(link => link.ProcessId).ToHashSet();
        foreach (var process in processes)
        {
            if (!existing.Contains(process.Id))
            {
                var link = await _riskStateRepository.InsertProcessLinkAsync(transaction, id, process, process.Id == primary);
                changes.Add((BusinessHistory.ProcessesPath, null, link));
            }
        }
    }

    /// <inheritdoc/>
    public async Task<RiskProcessOption[]> ListProcessesAsync(RiskActor actor, int offset, int limit)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.Create, transaction);
        var userId = actor.UserId;
        var allDirectorates = await HasRightAsync(actor, IdentityRights.AdminCode, transaction);
        var result = await ListProcessOptionsAsync(transaction, userId, allDirectorates, offset, limit);
        transaction.Commit();
        return result;
    }

    /// <inheritdoc/>
    public async Task<RiskProcessOption[]> ListSplitProcessesAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.Split, transaction);
        var risk = await ReadAsync<RiskDal>(id, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
        await _riskStateRepository.RequirePrimaryOwnerAsync(actor, id, transaction);
        if (risk.Status != RiskRules.Active || await _riskAssessmentStateRepository.ReadCurrentAssessmentAsync(id, transaction) is not { ResponseMode: null })
        {
            throw new IdentityCommandException(409, RiskRules.InvalidTransition);
        }
        var result = await ListProcessOptionsAsync(transaction, actor.UserId, true, offset, limit);
        transaction.Commit();
        return result;
    }

    /// <inheritdoc/>
    public async Task<RiskProcessOption[]> ListTransferProcessesAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, ReviewRules.TransferRight, transaction);
        var risk = await ReadAsync<RiskDal>(id, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
        await _riskStateRepository.RequirePrimaryOwnerAsync(actor, id, transaction);
        if (risk.Status != RiskRules.Active || await _riskAssessmentStateRepository.ReadCurrentAssessmentAsync(id, transaction) is not { ResponseMode: null })
        {
            throw new IdentityCommandException(409, RiskRules.InvalidTransition);
        }
        await _treatmentStateRepository.RequireNoBlockingTreatmentsAsync(transaction, id, includeDrafts: true);
        var currentProcesses = (await _riskStateRepository.ReadActiveRiskLinksAsync(id, transaction)).Select(link => link.ProcessId).ToArray();
        var result = await ListProcessOptionsAsync(transaction, actor.UserId, true, offset, limit, currentProcesses);
        transaction.Commit();
        return result;
    }

    /// <summary>Читает действующие процессы для выбора в форме</summary>
    /// <param name="transaction">Согласованный снимок</param>
    /// <param name="userId">Пользователь формы</param>
    /// <param name="allDirectorates">Доступны процессы всех дирекций</param>
    /// <param name="offset">Смещение страницы</param>
    /// <param name="limit">Размер страницы</param>
    /// <param name="excluded">Процессы, уже связанные с передаваемым риском</param>
    /// <returns>Допустимые процессы и их владельцы</returns>
    private async Task<RiskProcessOption[]> ListProcessOptionsAsync(IDbTransaction transaction, Guid userId,
        bool allDirectorates, int offset, int limit, Guid[]? excluded = null)
    {
        var query = $"""
            SELECT p.{EntityMapper.ColName<ProcessDal>(x => x.Id)}, p.{EntityMapper.ColName<ProcessDal>(x => x.Name)}, p.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)}, d.{EntityMapper.ColName<DirectorateDal>(x => x.Name)} AS {EntityMapper.ColName<RiskProcessOption>(x => x.DirectorateName)}, p.{EntityMapper.ColName<ProcessDal>(x => x.OwnerId)},
                u.{EntityMapper.ColName<UserProfileDal>(x => x.FullName)} AS {EntityMapper.ColName<RiskProcessOption>(x => x.OwnerName)}
            FROM {EntityMapper.TbName<ProcessDal>()} p
            JOIN {EntityMapper.TbName<DirectorateDal>()} d ON d.{EntityMapper.ColName<DirectorateDal>(x => x.Id)} = p.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)}
            JOIN {EntityMapper.TbName<OrgPersonDal>()} o ON o.{EntityMapper.ColName<OrgPersonDal>(x => x.Id)} = p.{EntityMapper.ColName<ProcessDal>(x => x.OwnerId)}
            JOIN {EntityMapper.TbName<UserProfileDal>()} u ON u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = o.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)}
            WHERE p.{EntityMapper.ColName<ProcessDal>(x => x.IsActive)} AND d.{EntityMapper.ColName<DirectorateDal>(x => x.IsActive)}
              AND o.{EntityMapper.ColName<OrgPersonDal>(x => x.IsActive)} AND u.{EntityMapper.ColName<UserProfileDal>(x => x.IsActive)}
              AND (CAST({EntityMapper.ParameterName(nameof(excluded))} AS uuid[]) IS NULL
                  OR NOT (p.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = ANY({EntityMapper.ParameterName(nameof(excluded))})))
              AND ({EntityMapper.ParameterName(nameof(allDirectorates))} OR EXISTS (SELECT FROM {EntityMapper.TbName<OrgPersonDal>()} member
                  WHERE member.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)} = {EntityMapper.ParameterName(nameof(userId))} AND member.{EntityMapper.ColName<OrgPersonDal>(x => x.IsActive)}
                    AND member.{EntityMapper.ColName<OrgPersonDal>(x => x.DirectorateId)} = p.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)}))
            ORDER BY p.{EntityMapper.ColName<ProcessDal>(x => x.Name)}, p.{EntityMapper.ColName<ProcessDal>(x => x.Id)} OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var result = await _connection.QueryAsync<RiskProcessOption>(query, new { allDirectorates, userId, offset, limit, excluded }, transaction);
        return result.ToArray();
    }

    /// <inheritdoc/>
    public async Task<AssetDal[]> ListAssetsAsync(RiskActor actor, int offset, int limit)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        if (!await HasRightAsync(actor, RiskRules.Create, transaction) &&
            !await HasRightAsync(actor, RiskRules.Edit, transaction))
        {
            throw new IdentityCommandException(403, IdentityErrorCodes.AccessDenied);
        }
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<AssetDal>()} WHERE {EntityMapper.ColName<AssetDal>(x => x.IsActive)}
              AND {EntityMapper.ColName<AssetDal>(x => x.HourlyDowntimeCost)} >= 0 AND {EntityMapper.ColName<AssetDal>(x => x.HourlyDowntimeCost)} = round({EntityMapper.ColName<AssetDal>(x => x.HourlyDowntimeCost)}, 2)
            ORDER BY {EntityMapper.ColName<AssetDal>(x => x.Name)}, {EntityMapper.ColName<AssetDal>(x => x.Id)} OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var result = await _connection.QueryAsync<AssetDal>(query, new { offset, limit }, transaction);
        transaction.Commit();
        return result.ToArray();
    }

    /// <inheritdoc/>
    public Task<RiskDal> CreateAsync(RiskActor actor, RiskPassportRequest request)
    {
        return WriteAsync(actor, RiskRules.Create, transaction => _riskStateRepository.CreateRiskAsync(actor, request, transaction));
    }

    /// <inheritdoc/>
    public Task<RiskDal> UpdateAsync(RiskActor actor, Guid id, RiskPassportRequest request)
    {
        return WriteAsync(actor, RiskRules.Edit, async transaction =>
        {
            var previous = await ReadAsync<RiskDal>(id, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
            await _riskStateRepository.RequirePrimaryOwnerAsync(actor, id, transaction);
            RequireVersion(previous.Version, request.Version);
            if (request.ProcessId is not null)
            {
                throw new IdentityCommandException(400, RiskRules.Invalid);
            }

            var assessmentQuery = $"SELECT EXISTS (SELECT FROM {EntityMapper.TbName<RiskAssessmentDal>()} WHERE {EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(id))})";
            if (await _connection.ExecuteScalarAsync<bool>(assessmentQuery, new { id }, transaction))
            {
                throw new IdentityCommandException(409, RiskRules.PassportLocked);
            }

            await _riskStateRepository.ValidatePassportAsync(request, previous, transaction);
            var name = RequiredText(request.Name);
            var description = RequiredText(request.Description);
            if (name == previous.Name && description == previous.Description && request.HazardId == previous.HazardId
                && request.CategoryId == previous.CategoryId && request.AssetId == previous.AssetId)
            {
                return previous;
            }

            var version = previous.Version;
            var query = $"""
                UPDATE {EntityMapper.TbName<RiskDal>()} SET {EntityMapper.ColName<RiskDal>(x => x.Name)} = {EntityMapper.ParameterName(nameof(name))}, {EntityMapper.ColName<RiskDal>(x => x.Description)} = {EntityMapper.ParameterName(nameof(description))},
                    {EntityMapper.ColName<RiskDal>(x => x.HazardId)} = {EntityMapper.ParameterName(nameof(request.HazardId))},
                    {EntityMapper.ColName<RiskDal>(x => x.CategoryId)} = {EntityMapper.ParameterName(nameof(request.CategoryId))},
                    {EntityMapper.ColName<RiskDal>(x => x.AssetId)} = {EntityMapper.ParameterName(nameof(request.AssetId))},
                    {EntityMapper.ColName<RiskDal>(x => x.Version)} = gen_random_uuid(), {EntityMapper.ColName<RiskDal>(x => x.UpdatedAt)} = clock_timestamp()
                WHERE {EntityMapper.ColName<RiskDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))} AND {EntityMapper.ColName<RiskDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(version))} RETURNING *
                """;
            var updated = await _connection.QuerySingleOrDefaultAsync<RiskDal>(query,
                new { id, version, name, description, request.HazardId, request.CategoryId, request.AssetId }, transaction)
                ?? throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
            await BusinessHistory.RecordAsync(transaction, Guid.NewGuid(), actor.UserId, id, previous, updated);
            return updated;
        });
    }

    /// <summary>Формат системного комментария о подтверждённой замене</summary>
    private const string OwnerChangedFormat = "Изменён владелец процесса «{0}»: {1} → {2}";

    /// <summary>Обновляет все текущие связи, версии рисков и системные комментарии</summary>
    /// <param name="transaction">Транзакция изменения процесса</param>
    /// <param name="actor">Инициатор</param>
    /// <param name="before">Процесс до изменения</param>
    /// <param name="after">Процесс после изменения</param>
    public async Task UpdateProcessOwnershipAsync(IDbTransaction transaction, Guid actor, ProcessDal before, ProcessDal after)
    {
        var db = transaction.Connection ?? throw new InvalidOperationException();
        var processId = after.Id;
        var ownerId = after.OwnerId;
        var operationId = Guid.NewGuid();
        await BusinessHistory.RecordAsync(transaction, operationId, actor, processId, before, after);

        var selectQuery = $"""
            SELECT * FROM {EntityMapper.TbName<RiskProcessDal>()} WHERE {EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)} = {EntityMapper.ParameterName(nameof(processId))} AND {EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL
            ORDER BY {EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)}
            """;
        var previousLinks = (await db.QueryAsync<RiskProcessDal>(selectQuery, new { processId }, transaction)).ToArray();
        if (previousLinks.Length == 0)
        {
            return;
        }

        var riskIds = previousLinks.Select(link => link.RiskId).Distinct().ToArray();
        var readRisksQuery = $"SELECT * FROM {EntityMapper.TbName<RiskDal>()} WHERE {EntityMapper.ColName<RiskDal>(x => x.Id)} = ANY({EntityMapper.ParameterName(nameof(riskIds))})";
        var previousRisks = (await db.QueryAsync<RiskDal>(readRisksQuery, new { riskIds }, transaction)).ToDictionary(risk => risk.Id);

        var updateLinksQuery = $"""
            UPDATE {EntityMapper.TbName<RiskProcessDal>()} SET {EntityMapper.ColName<RiskProcessDal>(x => x.OwnerId)} = {EntityMapper.ParameterName(nameof(ownerId))}, {EntityMapper.ColName<RiskProcessDal>(x => x.Version)} = gen_random_uuid()
            WHERE {EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)} = {EntityMapper.ParameterName(nameof(processId))} AND {EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL RETURNING *
            """;
        var links = (await db.QueryAsync<RiskProcessDal>(updateLinksQuery, new { processId, ownerId }, transaction))
            .ToDictionary(link => link.RiskId);
        var oldLinks = previousLinks.ToDictionary(link => link.RiskId);

        var updateRisksQuery = $"""
            UPDATE {EntityMapper.TbName<RiskDal>()} SET {EntityMapper.ColName<RiskDal>(x => x.Version)} = gen_random_uuid(), {EntityMapper.ColName<RiskDal>(x => x.UpdatedAt)} = clock_timestamp()
            WHERE {EntityMapper.ColName<RiskDal>(x => x.Id)} = ANY({EntityMapper.ParameterName(nameof(riskIds))}) RETURNING *
            """;
        var risks = await db.QueryAsync<RiskDal>(updateRisksQuery, new { riskIds }, transaction);
        var previousOwner = await ReadOwnerNameAsync(db, transaction, before.OwnerId);
        var nextOwner = await ReadOwnerNameAsync(db, transaction, ownerId);
        var comment = string.Format(System.Globalization.CultureInfo.InvariantCulture, OwnerChangedFormat,
            after.Name, previousOwner, nextOwner);
        foreach (var risk in risks)
        {
            await BusinessHistory.RecordAsync(transaction, operationId, actor, risk.Id, previousRisks[risk.Id], risk,
                relatedPath: BusinessHistory.ProcessesPath, relatedBefore: oldLinks[risk.Id], relatedAfter: links[risk.Id], system: true);
            await InsertCommentAsync(transaction, operationId, risk.Id, actor, comment, true);
        }
    }

    /// <summary>Считывает имя владельца для неизменяемого текста системного комментария</summary>
    /// <param name="db">Клиентская база</param>
    /// <param name="transaction">Транзакция замены</param>
    /// <param name="id">Сотрудник</param>
    /// <returns>Имя либо идентификатор при отсутствии имени</returns>
    private static async Task<string> ReadOwnerNameAsync(IDbConnection db, IDbTransaction transaction, Guid id)
    {
        var query = $"""
            SELECT u.{EntityMapper.ColName<UserProfileDal>(x => x.FullName)} FROM {EntityMapper.TbName<OrgPersonDal>()} p
            JOIN {EntityMapper.TbName<UserProfileDal>()} u ON u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = p.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)}
            WHERE p.{EntityMapper.ColName<OrgPersonDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))}
            """;
        return await db.QuerySingleOrDefaultAsync<string>(query, new { id }, transaction) ?? id.ToString();
    }

    /// <summary>Сортировка по времени обновления</summary>
    private const string RecentSort = "recent";

    /// <summary>Сортировка по названию</summary>
    private const string NameSort = "name";

    /// <summary>Общий отбор карточек и итогов в одном снимке</summary>
    private static readonly string FilteredRisks = $"""
            FROM {EntityMapper.TbName<RiskDal>()} r
            WHERE (CAST({EntityMapper.ParameterName(nameof(RiskDal.Id))} AS uuid) IS NULL OR r.{EntityMapper.ColName<RiskDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(RiskDal.Id))})
              AND strpos(lower(r.{EntityMapper.ColName<RiskDal>(x => x.Name)}), lower({EntityMapper.ParameterName(nameof(RiskListQuery.Search))})) > 0
              AND (CAST({EntityMapper.ParameterName(nameof(RiskListQuery.Status))} AS text) IS NULL OR r.{EntityMapper.ColName<RiskDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(RiskListQuery.Status))})
              AND (CAST({EntityMapper.ParameterName(nameof(RiskListQuery.AssetId))} AS uuid) IS NULL OR r.{EntityMapper.ColName<RiskDal>(x => x.AssetId)} = {EntityMapper.ParameterName(nameof(RiskListQuery.AssetId))})
              AND (CAST({EntityMapper.ParameterName(nameof(RiskListQuery.CategoryId))} AS uuid) IS NULL OR r.{EntityMapper.ColName<RiskDal>(x => x.CategoryId)} = {EntityMapper.ParameterName(nameof(RiskListQuery.CategoryId))})
              AND EXISTS (
                  SELECT FROM {EntityMapper.TbName<RiskProcessDal>()} l JOIN {EntityMapper.TbName<ProcessDal>()} p ON p.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = l.{EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)}
                  WHERE l.{EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)} = r.{EntityMapper.ColName<RiskDal>(x => x.Id)} AND l.{EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL
                    AND (CAST({EntityMapper.ParameterName(nameof(RiskListQuery.ProcessId))} AS uuid) IS NULL OR p.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(RiskListQuery.ProcessId))})
                    AND (CAST({EntityMapper.ParameterName(nameof(RiskListQuery.DirectorateId))} AS uuid) IS NULL OR p.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)} = {EntityMapper.ParameterName(nameof(RiskListQuery.DirectorateId))})
                    AND (CAST({EntityMapper.ParameterName(nameof(RiskListQuery.OwnerId))} AS uuid) IS NULL OR l.{EntityMapper.ColName<RiskProcessDal>(x => x.OwnerId)} = {EntityMapper.ParameterName(nameof(RiskListQuery.OwnerId))})
              )
        """;

    /// <inheritdoc/>
    public async Task<RiskRow[]> ListAsync(RiskActor actor, RiskListQuery filter)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        var result = await SelectAsync(filter, null, transaction);
        transaction.Commit();
        return result.Select(MapRow).ToArray();
    }

    /// <inheritdoc/>
    public async Task<RiskColumn[]> BoardAsync(RiskActor actor, RiskListQuery filter)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        var result = await SelectAsync(filter, null, transaction, board: true);
        var totals = await CountBoardAsync(filter, transaction);
        transaction.Commit();
        return RiskRules.BoardStatuses.Select(status =>
        {
            var items = result.Where(item => item.Status == status).Select(MapRow).ToArray();
            return new RiskColumn(status, items.LongLength, items) { TotalCount = totals.GetValueOrDefault(status) };
        }).ToArray();
    }

    /// <inheritdoc/>
    public async Task<RiskCard> GetAsync(RiskActor actor, Guid id)
    {
        using var transaction = _connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        var result = await SelectAsync(new RiskListQuery(), id, transaction);
        var row = result.SingleOrDefault() ?? throw new IdentityCommandException(404, RiskRules.NotFound);
        var data = MapRow(row);
        var last = await _reviewStateRepository.ReadLatestAssessmentAsync(id, transaction);
        var editable = last is null && await HasRightAsync(actor, RiskRules.Edit, transaction)
            && (await HasRightAsync(actor, IdentityRights.AdminCode, transaction) ||
                data.Processes.Any(process => process.IsPrimary && process.OwnerActive && process.OwnerUserId == actor.UserId));
        var reviews = await _reviewStateRepository.ReadRiskReviewsAsync(id, 0, ReviewRules.CardHistoryLimit, transaction);
        var jiraRetryPending = await _jiraStateRepository.HasRiskJiraRetryAsync(id, actor.UserId, transaction);
        transaction.Commit();
        return new RiskCard(data, editable, data.Risk.Status == RiskRules.AwaitingReview && last is { IsActive: false } ? last : null)
        {
            Reviews = reviews,
            JiraRetryPending = jiraRetryPending
        };
    }

    /// <summary>Выполняет фильтры до постраничной выборки</summary>
    /// <param name="filter">Параметры общей выборки</param>
    /// <param name="id">Опциональная выборка одной карточки</param>
    /// <param name="transaction">Согласованный снимок</param>
    /// <param name="board">Разбить окно на шесть независимых колонок</param>
    /// <returns>Строки выбранной страницы</returns>
    private async Task<RiskReadRow[]> SelectAsync(RiskListQuery filter, Guid? id, IDbTransaction transaction, bool board = false)
    {
        var order = filter.Sort switch
        {
            null or RecentSort => $"r.{EntityMapper.ColName<RiskDal>(x => x.UpdatedAt)} DESC, r.{EntityMapper.ColName<RiskDal>(x => x.Id)}",
            NameSort => $"lower(r.{EntityMapper.ColName<RiskDal>(x => x.Name)}), r.{EntityMapper.ColName<RiskDal>(x => x.Id)}",
            _ => throw new IdentityCommandException(400, RiskRules.Invalid)
        };
        if (filter.Status is { } requestedStatus && !RiskRules.BoardStatuses.Contains(requestedStatus))
        {
            throw new IdentityCommandException(400, RiskRules.Invalid);
        }

        var search = filter.Search?.Trim() ?? string.Empty;
        var partition = board ? $"PARTITION BY r.{EntityMapper.ColName<RiskDal>(x => x.Status)}" : string.Empty;
        var offset = filter.Offset;
        var limit = filter.Limit;
        var page = $"rn > {EntityMapper.ParameterName(nameof(offset))} AND rn <= CAST({EntityMapper.ParameterName(nameof(offset))} AS bigint) + {EntityMapper.ParameterName(nameof(limit))}";
        var query = $"""
            WITH filtered AS (
                SELECT r.*, row_number() OVER ({partition} ORDER BY {order}) AS rn
                {FilteredRisks}
            ), page AS (
                SELECT * FROM filtered WHERE {page}
            )
            SELECT r.{EntityMapper.ColName<RiskDal>(x => x.Status)} AS {EntityMapper.ColName<RiskReadRow>(x => x.Status)},
                   to_jsonb(r)::text AS {EntityMapper.ColName<RiskReadRow>(x => x.RiskJson)},
                   to_jsonb(a)::text AS {EntityMapper.ColName<RiskReadRow>(x => x.AssessmentJson)},
                   links.items AS {EntityMapper.ColName<RiskReadRow>(x => x.ProcessesJson)},
                   h.{EntityMapper.ColName<HazardDal>(x => x.Name)} AS {EntityMapper.ColName<RiskReadRow>(x => x.HazardName)},
                   cat.{EntityMapper.ColName<RiskCategoryDal>(x => x.Name)} AS {EntityMapper.ColName<RiskReadRow>(x => x.CategoryName)},
                   asset.{EntityMapper.ColName<AssetDal>(x => x.Name)} AS {EntityMapper.ColName<RiskReadRow>(x => x.AssetName)},
                   author.{EntityMapper.ColName<UserProfileDal>(x => x.FullName)} AS {EntityMapper.ColName<RiskReadRow>(x => x.AuthorName)}
            FROM page r
            LEFT JOIN {EntityMapper.TbName<HazardDal>()} h ON h.{EntityMapper.ColName<HazardDal>(x => x.Id)} = r.{EntityMapper.ColName<RiskDal>(x => x.HazardId)}
            LEFT JOIN {EntityMapper.TbName<RiskCategoryDal>()} cat ON cat.{EntityMapper.ColName<RiskCategoryDal>(x => x.Id)} = r.{EntityMapper.ColName<RiskDal>(x => x.CategoryId)}
            LEFT JOIN {EntityMapper.TbName<AssetDal>()} asset ON asset.{EntityMapper.ColName<AssetDal>(x => x.Id)} = r.{EntityMapper.ColName<RiskDal>(x => x.AssetId)}
            LEFT JOIN {EntityMapper.TbName<UserProfileDal>()} author ON author.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = r.{EntityMapper.ColName<RiskDal>(x => x.AuthorId)}
            LEFT JOIN {EntityMapper.TbName<RiskAssessmentDal>()} a ON a.{EntityMapper.ColName<RiskAssessmentDal>(x => x.RiskId)} = r.{EntityMapper.ColName<RiskDal>(x => x.Id)} AND a.{EntityMapper.ColName<RiskAssessmentDal>(x => x.IsActive)}
            LEFT JOIN LATERAL (
                SELECT jsonb_agg(to_jsonb(link_view) ORDER BY link_view.{EntityMapper.ColName<RiskProcessView>(x => x.IsPrimary)} DESC,
                    link_view.{EntityMapper.ColName<RiskProcessView>(x => x.ProcessId)})::text AS items
                FROM (
                    SELECT l.{EntityMapper.ColName<RiskProcessDal>(x => x.Id)} AS {EntityMapper.ColName<RiskProcessView>(x => x.RiskProcessId)},
                        p.{EntityMapper.ColName<ProcessDal>(x => x.Id)} AS {EntityMapper.ColName<RiskProcessView>(x => x.ProcessId)},
                        p.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)} AS {EntityMapper.ColName<RiskProcessView>(x => x.DirectorateId)},
                        l.{EntityMapper.ColName<RiskProcessDal>(x => x.OwnerId)} AS {EntityMapper.ColName<RiskProcessView>(x => x.OwnerId)},
                        l.{EntityMapper.ColName<RiskProcessDal>(x => x.IsPrimary)} AS {EntityMapper.ColName<RiskProcessView>(x => x.IsPrimary)},
                        p.{EntityMapper.ColName<ProcessDal>(x => x.Name)} AS {EntityMapper.ColName<RiskProcessView>(x => x.ProcessName)},
                        d.{EntityMapper.ColName<DirectorateDal>(x => x.Name)} AS {EntityMapper.ColName<RiskProcessView>(x => x.DirectorateName)},
                        CASE WHEN person.{EntityMapper.ColName<OrgPersonDal>(x => x.IsActive)} AND u.{EntityMapper.ColName<UserProfileDal>(x => x.IsActive)} THEN u.{EntityMapper.ColName<UserProfileDal>(x => x.FullName)} END
                            AS {EntityMapper.ColName<RiskProcessView>(x => x.OwnerName)},
                        person.{EntityMapper.ColName<OrgPersonDal>(x => x.IsActive)} AND u.{EntityMapper.ColName<UserProfileDal>(x => x.IsActive)} AS {EntityMapper.ColName<RiskProcessView>(x => x.OwnerActive)},
                        person.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)} AS {EntityMapper.ColName<RiskProcessView>(x => x.OwnerUserId)}
                    FROM {EntityMapper.TbName<RiskProcessDal>()} l JOIN {EntityMapper.TbName<ProcessDal>()} p ON p.{EntityMapper.ColName<ProcessDal>(x => x.Id)} = l.{EntityMapper.ColName<RiskProcessDal>(x => x.ProcessId)}
                    JOIN {EntityMapper.TbName<DirectorateDal>()} d ON d.{EntityMapper.ColName<DirectorateDal>(x => x.Id)} = p.{EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)}
                    JOIN {EntityMapper.TbName<OrgPersonDal>()} person ON person.{EntityMapper.ColName<OrgPersonDal>(x => x.Id)} = l.{EntityMapper.ColName<RiskProcessDal>(x => x.OwnerId)}
                    JOIN {EntityMapper.TbName<UserProfileDal>()} u ON u.{EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = person.{EntityMapper.ColName<OrgPersonDal>(x => x.UserId)}
                    WHERE l.{EntityMapper.ColName<RiskProcessDal>(x => x.RiskId)} = r.{EntityMapper.ColName<RiskDal>(x => x.Id)} AND l.{EntityMapper.ColName<RiskProcessDal>(x => x.UnlinkedAt)} IS NULL
                ) link_view
            ) links ON true
            ORDER BY r.rn, r.{EntityMapper.ColName<RiskDal>(x => x.Status)}
            """;
        var rows = await _connection.QueryAsync<RiskReadRow>(query,
            new { Id = id, Search = search, filter.Status, filter.AssetId, filter.CategoryId, filter.ProcessId,
                filter.DirectorateId, filter.OwnerId, offset, limit }, transaction);
        return rows.ToArray();
    }

    /// <summary>Считает подходящие риски по статусам до пагинации</summary>
    /// <param name="filter">Те же фильтры, что использует выборка карточек</param>
    /// <param name="transaction">Общий снимок с карточками доски</param>
    /// <returns>Число совпадений по статусу</returns>
    private async Task<Dictionary<string, long>> CountBoardAsync(RiskListQuery filter, IDbTransaction transaction)
    {
        var search = filter.Search?.Trim() ?? string.Empty;
        Guid? id = null;
        var status = EntityMapper.ColName<RiskDal>(x => x.Status);
        var query = $"""
            SELECT r.{status}, count(*)
            {FilteredRisks}
            GROUP BY r.{status}
            """;
        var rows = await _connection.QueryAsync<(string Status, long TotalCount)>(query,
            new { Id = id, Search = search, filter.Status, filter.AssetId, filter.CategoryId, filter.ProcessId,
                filter.DirectorateId, filter.OwnerId }, transaction);
        return rows.ToDictionary(row => row.Status, row => row.TotalCount);
    }

    /// <summary>Преобразует проекцию PostgreSQL в карточку реестра</summary>
    /// <param name="row">Строка из общего отбора</param>
    /// <returns>Паспорт, связи и исключительно текущая оценка</returns>
    private static RiskRow MapRow(RiskReadRow row)
    {
        var risk = JsonSerializer.Deserialize<RiskDal>(row.RiskJson ?? throw new InvalidOperationException(), JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException();
        var processes = row.ProcessesJson is { } links
            ? JsonSerializer.Deserialize<RiskProcessView[]>(links, JsonSerializerOptions.Web) ?? [] : [];
        var assessment = row.AssessmentJson is { } current
            ? JsonSerializer.Deserialize<RiskAssessmentDal>(current, JsonSerializerOptions.Web) : null;
        return new RiskRow(risk, processes, assessment, row.HazardName, row.CategoryName, row.AssetName, row.AuthorName);
    }

    /// <inheritdoc/>
    public async Task RecalculateAssetAsync(IDbTransaction transaction, Guid actor, Guid operationId, AssetDal asset)
    {
        var scale = await _riskAssessmentStateRepository.ReadScaleAsync(transaction);
        var settings = await _riskAssessmentStateRepository.ReadAcceptanceSettingsAsync(transaction);
        await _riskStateRepository.RecalculateAsync(transaction, actor, operationId, scale, settings, asset);
    }
}
