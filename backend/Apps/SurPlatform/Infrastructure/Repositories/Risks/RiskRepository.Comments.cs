using Infrastructure.Repositories.Risks.Helpers;
using System.Data;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.UseCases.Risks.Dto;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;

namespace Infrastructure.Repositories.Risks;

/// <summary>Комментарии и системные сообщения при смене состояния</summary>
internal sealed partial class RiskRepository
{
    /// <summary>Системный комментарий при закрытии</summary>
    private const string ClosedComment = "Риск закрыт. Причина: ";

    /// <summary>Системный комментарий при возврате закрытого риска</summary>
    private const string ReopenedComment = "Закрытый риск возвращён на переоценку";

    /// <inheritdoc/>
    public async Task<RiskCommentDal[]> ListCommentsAsync(RiskActor actor, Guid riskId, int offset, int limit)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        await ReadAsync<RiskDal>(riskId, EntityMapper.ColName<RiskDal>(x => x.Id), transaction);
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<RiskCommentDal>()} WHERE {EntityMapper.ColName<RiskCommentDal>(x => x.RiskId)} = {EntityMapper.ParameterName(nameof(riskId))} AND NOT {EntityMapper.ColName<RiskCommentDal>(x => x.IsDeleted)}
            ORDER BY {EntityMapper.ColName<RiskCommentDal>(x => x.CreatedAt)}, {EntityMapper.ColName<RiskCommentDal>(x => x.Id)} OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var rows = await connection.QueryAsync<RiskCommentDal>(query, new { riskId, offset, limit }, transaction);
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
            var updated = await connection.QuerySingleOrDefaultAsync<RiskCommentDal>(delete ? deleteQuery : editQuery,
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
            await RequirePrimaryOwnerAsync(actor, id, transaction);
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
                var reopened = await StartSingleReviewAsync(previous, ReviewRules.Manual, ReviewRules.RiskCard, id,
                    actor.UserId, operationId, transaction);
                if (previous.Status == RiskRules.Closed)
                {
                    await InsertCommentAsync(transaction, operationId, id, actor.UserId, ReopenedComment, true);
                }
                return reopened;
            }

            await RequireNoBlockingTreatmentsAsync(transaction, id);
            var reason = RequiredText(request.Reason);
            var status = RiskRules.Closed;
            if (previous.Status == RiskRules.AwaitingReview)
            {
                await CompleteReviewAsync(id, actor.UserId, transaction, null, reason);
            }

            var query = $"""
                UPDATE {EntityMapper.TbName<RiskDal>()} SET {EntityMapper.ColName<RiskDal>(x => x.Status)} = {EntityMapper.ParameterName(nameof(status))}, {EntityMapper.ColName<RiskDal>(x => x.CloseReason)} = {EntityMapper.ParameterName(nameof(reason))},
                    {EntityMapper.ColName<RiskDal>(x => x.Version)} = gen_random_uuid(), {EntityMapper.ColName<RiskDal>(x => x.UpdatedAt)} = clock_timestamp()
                WHERE {EntityMapper.ColName<RiskDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(id))} AND {EntityMapper.ColName<RiskDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(version))} RETURNING *
                """;
            var updated = await connection.QuerySingleAsync<RiskDal>(query, new { id, version, status, reason }, transaction);
            await BusinessHistory.RecordAsync(transaction, operationId, actor.UserId, id, previous, updated);
            await InsertCommentAsync(transaction, operationId, id, actor.UserId, ClosedComment + reason, true);
            return updated;
        });
    }
}
