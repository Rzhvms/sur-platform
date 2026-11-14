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

/// <summary>Комментарии к мерам с проверкой авторства</summary>
internal sealed partial class RiskRepository
{
    /// <inheritdoc/>
    public async Task<TreatmentCommentDal[]> ListTreatmentCommentsAsync(RiskActor actor, Guid id, int offset, int limit)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, RiskRules.View, transaction);
        await ReadAsync<TreatmentDal>(id, EntityMapper.ColName<TreatmentDal>(x => x.Id), transaction);
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<TreatmentCommentDal>()} WHERE {EntityMapper.ColName<TreatmentCommentDal>(x => x.TreatmentId)} = {EntityMapper.ParameterName(nameof(id))} AND NOT {EntityMapper.ColName<TreatmentCommentDal>(x => x.IsDeleted)}
            ORDER BY {EntityMapper.ColName<TreatmentCommentDal>(x => x.CreatedAt)}, {EntityMapper.ColName<TreatmentCommentDal>(x => x.Id)} OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var rows = await connection.QueryAsync<TreatmentCommentDal>(query, new { id, offset, limit }, transaction);
        transaction.Commit();
        return rows.ToArray();
    }

    /// <inheritdoc/>
    public Task<TreatmentCommentDal> SaveTreatmentCommentAsync(RiskActor actor, Guid id, Guid? commentId,
        TreatmentCommentRequest request, bool delete)
    {
        return WriteAsync(actor, TreatmentRules.Comment, async transaction =>
        {
            await RequireRightAsync(actor, RiskRules.View, transaction);
            await ReadAsync<TreatmentDal>(id, EntityMapper.ColName<TreatmentDal>(x => x.Id), transaction);
            var authorId = actor.UserId;
            TreatmentCommentDal? before = null;
            if (commentId.HasValue)
            {
                before = await ReadAsync<TreatmentCommentDal>(commentId.Value, EntityMapper.ColName<TreatmentCommentDal>(x => x.Id), transaction);
                if (before.IsDeleted || before.TreatmentId != id)
                {
                    throw new IdentityCommandException(404, RiskRules.NotFound);
                }
                if (before.AuthorId != authorId && !await HasRightAsync(actor, IdentityRights.AdminCode, transaction))
                {
                    throw new IdentityCommandException(403, IdentityErrorCodes.AccessDenied);
                }
                RequireVersion(before.Version, request.Version);
            }
            if (delete && before is null)
            {
                throw new IdentityCommandException(400, TreatmentRules.Invalid);
            }
            var text = delete ? before?.Text : RequiredText(request.Text);
            var row = new TreatmentCommentDal
            {
                Id = before?.Id ?? Guid.NewGuid(), TreatmentId = id, AuthorId = authorId,
                Text = text ?? string.Empty, Version = request.Version
            };
            var insertQuery = $"""
                INSERT INTO {EntityMapper.TbName<TreatmentCommentDal>()}
                    ({EntityMapper.ColName<TreatmentCommentDal>(x => x.Id)}, {EntityMapper.ColName<TreatmentCommentDal>(x => x.TreatmentId)}, {EntityMapper.ColName<TreatmentCommentDal>(x => x.AuthorId)}, {EntityMapper.ColName<TreatmentCommentDal>(x => x.Text)})
                VALUES ({EntityMapper.ParameterName(nameof(TreatmentCommentDal.Id))}, {EntityMapper.ParameterName(nameof(TreatmentCommentDal.TreatmentId))}, {EntityMapper.ParameterName(nameof(TreatmentCommentDal.AuthorId))}, {EntityMapper.ParameterName(nameof(TreatmentCommentDal.Text))}) RETURNING *
                """;
            var updateQuery = $"""
                UPDATE {EntityMapper.TbName<TreatmentCommentDal>()} SET {EntityMapper.ColName<TreatmentCommentDal>(x => x.Text)} = {EntityMapper.ParameterName(nameof(row.Text))},
                    {EntityMapper.ColName<TreatmentCommentDal>(x => x.UpdatedAt)} = CASE WHEN {EntityMapper.ParameterName(nameof(delete))} THEN {EntityMapper.ColName<TreatmentCommentDal>(x => x.UpdatedAt)} ELSE clock_timestamp() END,
                    {EntityMapper.ColName<TreatmentCommentDal>(x => x.IsDeleted)} = {EntityMapper.ParameterName(nameof(delete))}, {EntityMapper.ColName<TreatmentCommentDal>(x => x.Version)} = gen_random_uuid(),
                    {EntityMapper.ColName<TreatmentCommentDal>(x => x.DeletedAt)} = CASE WHEN {EntityMapper.ParameterName(nameof(delete))} THEN clock_timestamp() ELSE NULL END,
                    {EntityMapper.ColName<TreatmentCommentDal>(x => x.DeletedById)} = CASE WHEN {EntityMapper.ParameterName(nameof(delete))} THEN {EntityMapper.ParameterName(nameof(authorId))} ELSE NULL END
                WHERE {EntityMapper.ColName<TreatmentCommentDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(row.Id))} AND {EntityMapper.ColName<TreatmentCommentDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(row.Version))} RETURNING *
                """;
            var saved = before is null
                ? await connection.QuerySingleAsync<TreatmentCommentDal>(insertQuery, row, transaction)
                : await connection.QuerySingleOrDefaultAsync<TreatmentCommentDal>(updateQuery,
                    new { row.Id, row.Version, row.Text, delete, authorId }, transaction)
                    ?? throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
            await BusinessHistory.RecordAsync(transaction, Guid.NewGuid(), actor.UserId, saved.Id, before, saved);
            return saved;
        });
    }
}
