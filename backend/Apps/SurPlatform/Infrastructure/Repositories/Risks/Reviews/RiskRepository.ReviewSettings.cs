using System.Data;
using System.Security.Cryptography;
using System.Text;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.UseCases.Risks.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Repositories.Risks.Helpers;

namespace Infrastructure.Repositories.Risks;

/// <summary>Управление локальными настройками источников пересмотра</summary>
internal sealed partial class RiskRepository
{
    /// <summary>Длина случайного секрета в байтах</summary>
    private const int ReviewSecretBytes = ReviewRules.TokenLength / 2;

    /// <inheritdoc/>
    public Task<ReviewScheduleDal[]> ListReviewSchedulesAsync(RiskActor actor, int offset, int limit)
    {
        return ListReviewSettingsAsync<ReviewScheduleDal>(actor, offset, limit, EntityMapper.ColName<ReviewScheduleDal>(x => x.Id));
    }

    /// <inheritdoc/>
    public Task<ReviewWebhookDal[]> ListReviewWebhooksAsync(RiskActor actor, int offset, int limit)
    {
        return ListReviewSettingsAsync<ReviewWebhookDal>(actor, offset, limit, EntityMapper.ColName<ReviewWebhookDal>(x => x.Id));
    }

    /// <inheritdoc/>
    public Task<ReviewScheduleDal> SaveReviewScheduleAsync(RiskActor actor, Guid? id, ReviewScheduleRequest request, string timeZone)
    {
        return WriteAsync(actor, IdentityRights.AdminCode, async transaction =>
        {
            ReviewCalendar.Validate(request);
            var previous = id is Guid key
                ? await ReadAsync<ReviewScheduleDal>(key, EntityMapper.ColName<ReviewScheduleDal>(x => x.Id), transaction) : null;
            if (previous is not null)
            {
                RequireVersion(previous.Version, request.Version);
            }
            await RequireReviewResponsibleAsync(request.ResponsibleUserId, transaction);
            var zone = previous?.TimeZone ?? timeZone;
            try
            {
                _ = TimeZoneInfo.FindSystemTimeZoneById(zone);
            }
            catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                throw new IdentityCommandException(400, ReviewRules.Invalid);
            }

            var now = DateTime.UtcNow;
            var row = new ReviewScheduleDal
            {
                Id = previous?.Id ?? Guid.NewGuid(), Version = Guid.NewGuid(), Name = RequiredText(request.Name),
                IsActive = request.IsActive, ResponsibleUserId = request.ResponsibleUserId,
                TimeZone = zone, StartDate = request.StartDate.ToDateTime(TimeOnly.MinValue),
                RecurrenceType = request.RecurrenceType, RecurrenceInterval = request.RecurrenceInterval,
                Weekdays = request.Weekdays ?? [], EndType = request.EndType, OccurrenceCount = request.OccurrenceCount,
                EndDate = request.EndDate?.ToDateTime(TimeOnly.MinValue),
                CreatedAt = previous?.CreatedAt ?? now, CreatedById = previous?.CreatedById ?? actor.UserId,
                UpdatedAt = now, UpdatedById = actor.UserId
            };
            var insertQuery = $"""
                INSERT INTO {EntityMapper.TbName<ReviewScheduleDal>()}
                    ({EntityMapper.ColName<ReviewScheduleDal>(x => x.Id)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.Version)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.Name)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.IsActive)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.ResponsibleUserId)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.CreatedById)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.UpdatedById)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.CreatedAt)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.UpdatedAt)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.TimeZone)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.StartDate)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.RecurrenceType)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.RecurrenceInterval)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.Weekdays)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.EndType)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.OccurrenceCount)}, {EntityMapper.ColName<ReviewScheduleDal>(x => x.EndDate)})
                VALUES ({EntityMapper.ParameterName(nameof(row.Id))}, {EntityMapper.ParameterName(nameof(row.Version))}, {EntityMapper.ParameterName(nameof(row.Name))}, {EntityMapper.ParameterName(nameof(row.IsActive))}, {EntityMapper.ParameterName(nameof(row.ResponsibleUserId))}, {EntityMapper.ParameterName(nameof(row.CreatedById))}, {EntityMapper.ParameterName(nameof(row.UpdatedById))}, {EntityMapper.ParameterName(nameof(row.CreatedAt))}, {EntityMapper.ParameterName(nameof(row.UpdatedAt))}, {EntityMapper.ParameterName(nameof(row.TimeZone))}, {EntityMapper.ParameterName(nameof(row.StartDate))}, {EntityMapper.ParameterName(nameof(row.RecurrenceType))}, {EntityMapper.ParameterName(nameof(row.RecurrenceInterval))}, {EntityMapper.ParameterName(nameof(row.Weekdays))}, {EntityMapper.ParameterName(nameof(row.EndType))}, {EntityMapper.ParameterName(nameof(row.OccurrenceCount))}, {EntityMapper.ParameterName(nameof(row.EndDate))}) RETURNING *
                """;
            var updateQuery = $"""
                UPDATE {EntityMapper.TbName<ReviewScheduleDal>()} SET {EntityMapper.ColName<ReviewScheduleDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(row.Version))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.UpdatedAt)} = {EntityMapper.ParameterName(nameof(row.UpdatedAt))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.UpdatedById)} = {EntityMapper.ParameterName(nameof(row.UpdatedById))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.Name)} = {EntityMapper.ParameterName(nameof(row.Name))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.IsActive)} = {EntityMapper.ParameterName(nameof(row.IsActive))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.ResponsibleUserId)} = {EntityMapper.ParameterName(nameof(row.ResponsibleUserId))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.StartDate)} = {EntityMapper.ParameterName(nameof(row.StartDate))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.RecurrenceType)} = {EntityMapper.ParameterName(nameof(row.RecurrenceType))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.RecurrenceInterval)} = {EntityMapper.ParameterName(nameof(row.RecurrenceInterval))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.Weekdays)} = {EntityMapper.ParameterName(nameof(row.Weekdays))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.EndType)} = {EntityMapper.ParameterName(nameof(row.EndType))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.OccurrenceCount)} = {EntityMapper.ParameterName(nameof(row.OccurrenceCount))},
                    {EntityMapper.ColName<ReviewScheduleDal>(x => x.EndDate)} = {EntityMapper.ParameterName(nameof(row.EndDate))}
                WHERE {EntityMapper.ColName<ReviewScheduleDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(row.Id))} RETURNING *
                """;
            return await connection.QuerySingleAsync<ReviewScheduleDal>(previous is null ? insertQuery : updateQuery, row, transaction);
        });
    }

    /// <inheritdoc/>
    public Task<bool> DeleteReviewScheduleAsync(RiskActor actor, Guid id, Guid version)
    {
        return DeleteReviewSettingAsync<ReviewScheduleDal>(actor, id, version, EntityMapper.ColName<ReviewScheduleDal>(x => x.Id),
            EntityMapper.ColName<ReviewScheduleDal>(x => x.Version));
    }

    /// <inheritdoc/>
    public Task<bool> DeleteReviewWebhookAsync(RiskActor actor, Guid id, Guid version)
    {
        return DeleteReviewSettingAsync<ReviewWebhookDal>(actor, id, version, EntityMapper.ColName<ReviewWebhookDal>(x => x.Id),
            EntityMapper.ColName<ReviewWebhookDal>(x => x.Version));
    }

    /// <inheritdoc/>
    public async Task<ReviewWebhookView> GetReviewWebhookAsync(RiskActor actor, Guid id)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, IdentityRights.AdminCode, transaction);
        var row = await ReadAsync<ReviewWebhookDal>(id, EntityMapper.ColName<ReviewWebhookDal>(x => x.Id), transaction);
        var targets = await ReadWebhookTargetsAsync(id, transaction);
        transaction.Commit();
        return new ReviewWebhookView(row, targets);
    }

    /// <inheritdoc/>
    public Task<ReviewWebhookReceipt> CreateReviewWebhookAsync(RiskActor actor, ReviewWebhookRequest request)
    {
        return WriteAsync(actor, IdentityRights.AdminCode, async transaction =>
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(ReviewSecretBytes));
            var webhook = await SaveReviewWebhookAsync(actor, null, request, HashReviewToken(token), transaction);
            return new ReviewWebhookReceipt(webhook, token);
        });
    }

    /// <inheritdoc/>
    public Task<ReviewWebhookView> UpdateReviewWebhookAsync(RiskActor actor, Guid id, ReviewWebhookRequest request)
    {
        return WriteAsync(actor, IdentityRights.AdminCode, transaction => SaveReviewWebhookAsync(actor, id, request, null, transaction));
    }

    /// <inheritdoc/>
    public Task<ReviewWebhookReceipt> RotateReviewWebhookAsync(RiskActor actor, Guid id, RotateReviewTokenRequest request)
    {
        return WriteAsync(actor, IdentityRights.AdminCode, async transaction =>
        {
            var previous = await ReadAsync<ReviewWebhookDal>(id, EntityMapper.ColName<ReviewWebhookDal>(x => x.Id), transaction);
            RequireVersion(previous.Version, request.Version);
            if (!request.Confirmed)
            {
                throw new IdentityCommandException(400, ReviewRules.Invalid);
            }
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(ReviewSecretBytes));
            var row = previous with { TokenHash = HashReviewToken(token), Version = Guid.NewGuid(), UpdatedById = actor.UserId, UpdatedAt = DateTime.UtcNow };
            var query = $"""
                UPDATE {EntityMapper.TbName<ReviewWebhookDal>()} SET {EntityMapper.ColName<ReviewWebhookDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(row.Version))},
                    {EntityMapper.ColName<ReviewWebhookDal>(x => x.UpdatedAt)} = {EntityMapper.ParameterName(nameof(row.UpdatedAt))},
                    {EntityMapper.ColName<ReviewWebhookDal>(x => x.UpdatedById)} = {EntityMapper.ParameterName(nameof(row.UpdatedById))},
                    {EntityMapper.ColName<ReviewWebhookDal>(x => x.TokenHash)} = {EntityMapper.ParameterName(nameof(row.TokenHash))}
                WHERE {EntityMapper.ColName<ReviewWebhookDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(row.Id))} RETURNING *
                """;
            var updated = await connection.QuerySingleAsync<ReviewWebhookDal>(query, row, transaction);
            return new ReviewWebhookReceipt(new ReviewWebhookView(updated, await ReadWebhookTargetsAsync(id, transaction)), token);
        });
    }

    /// <summary>Сохраняет настройки и меняет только добавленные или удалённые связи</summary>
    /// <param name="actor">Администратор</param>
    /// <param name="id">Настройка при редактировании</param>
    /// <param name="request">Поля настройки</param>
    /// <param name="hash">Хеш при регистрации нового источника</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <returns>Настройка и её область</returns>
    private async Task<ReviewWebhookView> SaveReviewWebhookAsync(RiskActor actor, Guid? id, ReviewWebhookRequest request, byte[]? hash, IDbTransaction transaction)
    {
        var previous = id is Guid key
            ? await ReadAsync<ReviewWebhookDal>(key, EntityMapper.ColName<ReviewWebhookDal>(x => x.Id), transaction) : null;
        if (previous is not null)
        {
            RequireVersion(previous.Version, request.Version);
        }
        await RequireReviewResponsibleAsync(request.ResponsibleUserId, transaction);
        var oldTargets = previous is null ? [] : await ReadWebhookTargetsAsync(previous.Id, transaction);
        var targets = request.RiskIds ?? [];
        if (targets.Length == 0 || targets.Contains(Guid.Empty) || targets.Distinct().Count() != targets.Length)
        {
            throw new IdentityCommandException(400, ReviewRules.Invalid);
        }

        var added = targets.Except(oldTargets).ToArray();
        if (added.Length > 0 && (await ReadAcceptedRiskIdsAsync(transaction, added)).Length != added.Length)
        {
            throw new IdentityCommandException(409, RiskRules.InvalidTransition);
        }

        var now = DateTime.UtcNow;
        var row = new ReviewWebhookDal
        {
            Id = previous?.Id ?? Guid.NewGuid(), Version = Guid.NewGuid(), Name = RequiredText(request.Name),
            ExternalSystemName = RequiredText(request.ExternalSystemName), IsActive = request.IsActive,
            ResponsibleUserId = request.ResponsibleUserId, TokenHash = previous?.TokenHash ?? hash
                ?? throw new InvalidOperationException(ReviewRules.Invalid),
            CreatedAt = previous?.CreatedAt ?? now, CreatedById = previous?.CreatedById ?? actor.UserId,
            UpdatedAt = now, UpdatedById = actor.UserId
        };
        var insertQuery = $"""
            INSERT INTO {EntityMapper.TbName<ReviewWebhookDal>()}
                ({EntityMapper.ColName<ReviewWebhookDal>(x => x.Id)}, {EntityMapper.ColName<ReviewWebhookDal>(x => x.Version)}, {EntityMapper.ColName<ReviewWebhookDal>(x => x.Name)}, {EntityMapper.ColName<ReviewWebhookDal>(x => x.IsActive)}, {EntityMapper.ColName<ReviewWebhookDal>(x => x.ResponsibleUserId)}, {EntityMapper.ColName<ReviewWebhookDal>(x => x.CreatedById)}, {EntityMapper.ColName<ReviewWebhookDal>(x => x.UpdatedById)}, {EntityMapper.ColName<ReviewWebhookDal>(x => x.CreatedAt)}, {EntityMapper.ColName<ReviewWebhookDal>(x => x.UpdatedAt)}, {EntityMapper.ColName<ReviewWebhookDal>(x => x.ExternalSystemName)}, {EntityMapper.ColName<ReviewWebhookDal>(x => x.TokenHash)})
            VALUES ({EntityMapper.ParameterName(nameof(row.Id))}, {EntityMapper.ParameterName(nameof(row.Version))}, {EntityMapper.ParameterName(nameof(row.Name))}, {EntityMapper.ParameterName(nameof(row.IsActive))}, {EntityMapper.ParameterName(nameof(row.ResponsibleUserId))}, {EntityMapper.ParameterName(nameof(row.CreatedById))}, {EntityMapper.ParameterName(nameof(row.UpdatedById))}, {EntityMapper.ParameterName(nameof(row.CreatedAt))}, {EntityMapper.ParameterName(nameof(row.UpdatedAt))}, {EntityMapper.ParameterName(nameof(row.ExternalSystemName))}, {EntityMapper.ParameterName(nameof(row.TokenHash))}) RETURNING *
            """;
        var updateQuery = $"""
            UPDATE {EntityMapper.TbName<ReviewWebhookDal>()} SET {EntityMapper.ColName<ReviewWebhookDal>(x => x.Version)} = {EntityMapper.ParameterName(nameof(row.Version))},
                {EntityMapper.ColName<ReviewWebhookDal>(x => x.UpdatedAt)} = {EntityMapper.ParameterName(nameof(row.UpdatedAt))},
                {EntityMapper.ColName<ReviewWebhookDal>(x => x.UpdatedById)} = {EntityMapper.ParameterName(nameof(row.UpdatedById))},
                {EntityMapper.ColName<ReviewWebhookDal>(x => x.Name)} = {EntityMapper.ParameterName(nameof(row.Name))},
                {EntityMapper.ColName<ReviewWebhookDal>(x => x.ExternalSystemName)} = {EntityMapper.ParameterName(nameof(row.ExternalSystemName))},
                {EntityMapper.ColName<ReviewWebhookDal>(x => x.IsActive)} = {EntityMapper.ParameterName(nameof(row.IsActive))},
                {EntityMapper.ColName<ReviewWebhookDal>(x => x.ResponsibleUserId)} = {EntityMapper.ParameterName(nameof(row.ResponsibleUserId))}
            WHERE {EntityMapper.ColName<ReviewWebhookDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(row.Id))} RETURNING *
            """;
        var saved = await connection.QuerySingleAsync<ReviewWebhookDal>(previous is null ? insertQuery : updateQuery, row, transaction);
        var removed = oldTargets.Except(targets).ToArray();
        await UpdateWebhookTargetsAsync(saved.Id, added, removed, actor.UserId, transaction);
        return new ReviewWebhookView(saved, targets);
    }

    /// <summary>Изменяет разницу набора целей с сохранением автора неизменённых связей</summary>
    /// <param name="id">Настройка</param>
    /// <param name="added">Добавленные цели</param>
    /// <param name="removed">Удалённые цели</param>
    /// <param name="actor">Автор настройки</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    private async Task UpdateWebhookTargetsAsync(Guid id, Guid[] added, Guid[] removed, Guid actor, IDbTransaction transaction)
    {
        if (removed.Length > 0)
        {
            var deleteQuery = $"""
                DELETE FROM {EntityMapper.TbName<ReviewWebhookRiskDal>()} WHERE {EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.WebhookId)} = {EntityMapper.ParameterName(nameof(id))} AND {EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.RiskId)} = ANY({EntityMapper.ParameterName(nameof(removed))})
                """;
            await connection.ExecuteAsync(deleteQuery, new { id, removed }, transaction);
        }
        if (added.Length > 0)
        {
            var insertQuery = $"""
                INSERT INTO {EntityMapper.TbName<ReviewWebhookRiskDal>()} ({EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.WebhookId)}, {EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.RiskId)}, {EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.LinkedAt)}, {EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.LinkedById)})
                SELECT {EntityMapper.ParameterName(nameof(id))}, target, clock_timestamp(), {EntityMapper.ParameterName(nameof(actor))} FROM unnest({EntityMapper.ParameterName(nameof(added))}) AS target
                """;
            await connection.ExecuteAsync(insertQuery, new { id, added, actor }, transaction);
        }
    }

    /// <summary>Читает область ранее сохранённой настройки</summary>
    /// <param name="id">Настройка</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    /// <returns>Цели настройки</returns>
    private async Task<Guid[]> ReadWebhookTargetsAsync(Guid id, IDbTransaction transaction)
    {
        var query = $"""
            SELECT {EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.RiskId)} FROM {EntityMapper.TbName<ReviewWebhookRiskDal>()} WHERE {EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.WebhookId)} = {EntityMapper.ParameterName(nameof(id))} ORDER BY {EntityMapper.ColName<ReviewWebhookRiskDal>(x => x.RiskId)}
            """;
        return (await connection.QueryAsync<Guid>(query, new { id }, transaction)).ToArray();
    }

    /// <summary>Проверяет действующего ответственного</summary>
    /// <param name="userId">Идентификатор пользователя</param>
    /// <param name="transaction">Текущая клиентская транзакция</param>
    private async Task RequireReviewResponsibleAsync(Guid userId, IDbTransaction transaction)
    {
        var query = $"""
            SELECT EXISTS (SELECT FROM {EntityMapper.TbName<UserProfileDal>()} WHERE {EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = {EntityMapper.ParameterName(nameof(userId))} AND {EntityMapper.ColName<UserProfileDal>(x => x.IsActive)})
            """;
        if (!await connection.ExecuteScalarAsync<bool>(query, new { userId }, transaction))
        {
            throw new IdentityCommandException(400, ReviewRules.Invalid);
        }
    }

    /// <summary>Читает страницу настроек с актуальным правом администратора</summary>
    /// <typeparam name="T">Тип настройки</typeparam>
    /// <param name="actor">Администратор</param>
    /// <param name="offset">Смещение</param>
    /// <param name="limit">Размер страницы</param>
    /// <param name="idColumn">Ключ EntityMapper</param>
    /// <returns>Настройки</returns>
    private async Task<T[]> ListReviewSettingsAsync<T>(RiskActor actor, int offset, int limit, string idColumn)
    {
        using var transaction = connection.BeginTransaction(IsolationLevel.RepeatableRead);
        await RequireRightAsync(actor, IdentityRights.AdminCode, transaction);
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<T>()} ORDER BY {idColumn} OFFSET {EntityMapper.ParameterName(nameof(offset))} LIMIT {EntityMapper.ParameterName(nameof(limit))}
            """;
        var rows = (await connection.QueryAsync<T>(query, new { offset, limit }, transaction)).ToArray();
        transaction.Commit();
        return rows;
    }

    /// <summary>Удаляет только актуальную версию настройки, сохраняя запуски</summary>
    /// <typeparam name="T">Тип настройки</typeparam>
    /// <param name="actor">Администратор</param>
    /// <param name="id">Настройка</param>
    /// <param name="version">Ожидаемая версия</param>
    /// <param name="idColumn">Колонка ключа</param>
    /// <param name="versionColumn">Колонка версии</param>
    /// <returns>Признак удаления</returns>
    private Task<bool> DeleteReviewSettingAsync<T>(RiskActor actor, Guid id, Guid version, string idColumn, string versionColumn)
    {
        return WriteAsync(actor, IdentityRights.AdminCode, async transaction =>
        {
            var query = $"""
                DELETE FROM {EntityMapper.TbName<T>()} WHERE {idColumn} = {EntityMapper.ParameterName(nameof(id))} AND {versionColumn} = {EntityMapper.ParameterName(nameof(version))}
                """;
            if (await connection.ExecuteAsync(query, new { id, version }, transaction) != 1)
            {
                throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
            }
            return true;
        });
    }

    /// <summary>Хеширует случайный высокоэнтропийный секрет</summary>
    /// <param name="token">Bearer-секрет</param>
    /// <returns>SHA-256 секрета</returns>
    private static byte[] HashReviewToken(string token)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }
}
