using Microsoft.AspNetCore.DataProtection;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.UseCases.Jira.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Repositories.Risks.Helpers;
using Infrastructure.Services.Jira;

namespace Infrastructure.Repositories.Risks;

/// <summary>Административные настройки и защита реквизитов Jira</summary>
internal sealed partial class RiskRepository
{
    /// <summary>Читает настройку выбранной клиентской базы</summary>
    /// <param name="transaction">Снимок записи</param>
    /// <returns>Настройка, если создана</returns>
    private Task<JiraSettingsDal?> ReadJiraAsync(IDbTransaction? transaction = null)
    {
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<JiraSettingsDal>()}
            """;
        return connection.QuerySingleOrDefaultAsync<JiraSettingsDal>(query, transaction: transaction);
    }

    /// <inheritdoc/>
    public Task<JiraSettingsDal?> GetJiraAsync(RiskActor actor)
    {
        return WriteAsync(actor, IdentityRights.AdminCode, ReadJiraAsync);
    }

    /// <inheritdoc/>
    public async Task<JiraSettingsDal> SaveJiraAsync(RiskActor actor, JiraSettingsRequest request, CancellationToken cancellation)
    {
        var previous = await GetJiraAsync(actor);
        if ((previous?.Version ?? Guid.Empty) != request.Version)
        {
            throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
        }
        var address = JiraRestClient.Resolve(jiraConfiguration, RequiredText(request.BaseUrl));
        var project = RequiredText(request.Project);
        var issueType = RequiredText(request.IssueType);
        if (!JiraRestClient.IsResourceId(project) || !JiraRestClient.IsResourceId(issueType))
        {
            throw new IdentityCommandException(400, JiraRules.Invalid);
        }
        var token = request.RestToken ?? (previous is not null && previous.BaseUrl == address.AbsoluteUri
            ? JiraProtector().Unprotect(previous.RestSecret) : null);
        if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsWhiteSpace) ||
            request.WebhookSecret is { Length: < JiraRules.MinWebhookSecretLength or > JiraRules.MaxWebhookSecretLength } ||
            request.Executors is null || request.Executors.Values.Any(string.IsNullOrWhiteSpace) ||
            request.Executors.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Executors.Count)
        {
            throw new IdentityCommandException(400, JiraRules.Invalid);
        }
        var hash = request.WebhookSecret is null ? previous?.WebhookHash : HashJiraSecret(request.WebhookSecret);
        if (hash is null || CryptographicOperations.FixedTimeEquals(hash, HashJiraSecret(token)))
        {
            throw new IdentityCommandException(400, JiraRules.Invalid);
        }
        var zone = previous?.TimeZone ?? jiraConfiguration["Application:TimeZone"] ?? TimeZoneInfo.Utc.Id;
        _ = TimeZoneInfo.FindSystemTimeZoneById(zone);
        var row = new JiraSettingsDal
        {
            Id = Guid.Empty, Version = Guid.NewGuid(), BaseUrl = address.AbsoluteUri, Project = project,
            IssueType = issueType, DoneStatus = request.DoneStatus, HoldStatus = request.HoldStatus,
            CancelStatus = request.CancelStatus, Enabled = request.Enabled, ResponsibleUserId = request.ResponsibleUserId,
            UpdatedById = actor.UserId, RestSecret = JiraProtector().Protect(token), WebhookHash = hash, TimeZone = zone,
            Executors = JsonSerializer.Serialize(request.Executors)
        };
        if (row.Enabled)
        {
            await ValidateJiraAsync(row, token, cancellation);
            await VerifyJiraExecutorMappingsAsync(row, token, cancellation);
        }

        return await WriteAsync(actor, IdentityRights.AdminCode, async transaction =>
        {
            var current = await ReadJiraAsync(transaction);
            if ((current?.Version ?? Guid.Empty) != request.Version)
            {
                throw new IdentityCommandException(409, IdentityErrorCodes.StaleVersion);
            }
            if (current is not null && (current.BaseUrl != row.BaseUrl || current.Project != row.Project || current.IssueType != row.IssueType))
            {
                var sending = JiraRules.Sending;
                var unknown = JiraRules.Unknown;
                var used = $"""
                    SELECT EXISTS (SELECT FROM {EntityMapper.TbName<JiraDeliveryDal>()} WHERE {EntityMapper.ColName<JiraDeliveryDal>(x => x.IssueId)} IS NOT NULL OR {EntityMapper.ColName<JiraDeliveryDal>(x => x.State)} IN ({EntityMapper.ParameterName(nameof(sending))}, {EntityMapper.ParameterName(nameof(unknown))}))
                    """;
                if (await connection.ExecuteScalarAsync<bool>(used, new { sending, unknown }, transaction))
                {
                    throw new IdentityCommandException(409, JiraRules.Conflict);
                }
            }
            await RequireReviewResponsibleAsync(row.ResponsibleUserId, transaction);
            var query = $"""
                INSERT INTO {EntityMapper.TbName<JiraSettingsDal>()} ({EntityMapper.ColName<JiraSettingsDal>(x => x.Id)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.Version)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.BaseUrl)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.Project)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.IssueType)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.DoneStatus)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.HoldStatus)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.CancelStatus)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.Enabled)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.ResponsibleUserId)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.UpdatedById)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.RestSecret)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.WebhookHash)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.TimeZone)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.Executors)})
                VALUES ({EntityMapper.ParameterName(nameof(row.Id))}, {EntityMapper.ParameterName(nameof(row.Version))}, {EntityMapper.ParameterName(nameof(row.BaseUrl))}, {EntityMapper.ParameterName(nameof(row.Project))}, {EntityMapper.ParameterName(nameof(row.IssueType))}, {EntityMapper.ParameterName(nameof(row.DoneStatus))}, {EntityMapper.ParameterName(nameof(row.HoldStatus))}, {EntityMapper.ParameterName(nameof(row.CancelStatus))}, {EntityMapper.ParameterName(nameof(row.Enabled))}, {EntityMapper.ParameterName(nameof(row.ResponsibleUserId))}, {EntityMapper.ParameterName(nameof(row.UpdatedById))}, {EntityMapper.ParameterName(nameof(row.RestSecret))}, {EntityMapper.ParameterName(nameof(row.WebhookHash))}, {EntityMapper.ParameterName(nameof(row.TimeZone))}, {EntityMapper.ParameterName(nameof(row.Executors))})
                ON CONFLICT ({EntityMapper.ColName<JiraSettingsDal>(x => x.Id)}) DO UPDATE SET {EntityMapper.ColName<JiraSettingsDal>(x => x.Version)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.Version)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.BaseUrl)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.BaseUrl)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.Project)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.Project)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.IssueType)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.IssueType)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.DoneStatus)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.DoneStatus)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.HoldStatus)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.HoldStatus)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.CancelStatus)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.CancelStatus)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.Enabled)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.Enabled)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.ResponsibleUserId)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.ResponsibleUserId)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.UpdatedById)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.UpdatedById)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.RestSecret)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.RestSecret)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.WebhookHash)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.WebhookHash)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.TimeZone)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.TimeZone)}, {EntityMapper.ColName<JiraSettingsDal>(x => x.Executors)} = EXCLUDED.{EntityMapper.ColName<JiraSettingsDal>(x => x.Executors)}
                """;
            await connection.ExecuteAsync(query, row, transaction);
            if (row.Enabled)
            {
                await RestoreJiraAsync(actor.UserId, transaction);
            }
            return row;
        });
    }

    /// <inheritdoc/>
    public async Task<JsonElement> DiscoverJiraAsync(RiskActor actor, JiraDiscoveryRequest request, CancellationToken cancellation)
    {
        var previous = await GetJiraAsync(actor);
        var address = JiraRestClient.Resolve(jiraConfiguration, RequiredText(request.BaseUrl));
        var token = request.RestToken ?? (previous is not null && previous.BaseUrl == address.AbsoluteUri
            ? JiraProtector().Unprotect(previous.RestSecret) : null);
        if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsWhiteSpace))
        {
            throw new IdentityCommandException(400, JiraRules.Invalid);
        }
        try
        {
            const string projectsPath = "project";
            if (request.Project is not { } key)
            {
                var projects = await jiraRest.RequestAsync(address, token, projectsPath, cancellation);
                return JsonSerializer.SerializeToElement(new
                {
                    projects = projects.EnumerateArray().Select(project => new
                    {
                        id = project.GetProperty("id").GetString(),
                        key = project.GetProperty("key").GetString(),
                        name = project.GetProperty("name").GetString()
                    })
                });
            }
            if (!JiraRestClient.IsResourceId(key))
            {
                throw new IdentityCommandException(400, JiraRules.Invalid);
            }
            var projectId = Uri.EscapeDataString(key);
            var issueTypes = await jiraRest.MetadataAsync(address, token, $"issue/createmeta/{projectId}/issuetypes", cancellation);
            var statuses = await jiraRest.RequestAsync(address, token, $"{projectsPath}/{projectId}/statuses", cancellation);
            return JsonSerializer.SerializeToElement(new { issueTypes, statuses });
        }
        catch (Exception error) when (error is not IdentityCommandException && !cancellation.IsCancellationRequested && IsJiraFailure(error))
        {
            throw new IdentityCommandException(400, JiraRules.Unavailable);
        }
    }

    /// <summary>Проверяет поддерживаемые поля, статусы и право создавать задачи</summary>
    /// <param name="row">Предлагаемая настройка</param>
    /// <param name="token">Секрет доступа</param>
    /// <param name="cancellation">Отмена</param>
    private async Task ValidateJiraAsync(JiraSettingsDal row, string token, CancellationToken cancellation)
    {

        try
        {
            var uri = JiraRestClient.Resolve(jiraConfiguration, row.BaseUrl);
            var project = Uri.EscapeDataString(row.Project);
            var issueType = Uri.EscapeDataString(row.IssueType);
            var prefix = $"issue/createmeta/{project}/issuetypes";
            var types = await jiraRest.MetadataAsync(uri, token, prefix, cancellation);
            if (!types.Any(type => type.GetProperty("id").GetString() == row.IssueType &&
                (!type.TryGetProperty("subtask", out var subtask) || !subtask.GetBoolean())))
            {
                throw new IdentityCommandException(400, JiraRules.Invalid);
            }
            var fields = await jiraRest.MetadataAsync(uri, token, prefix + "/" + issueType, cancellation);
            var names = new HashSet<string>(StringComparer.Ordinal) { "project", "issuetype", "summary", "assignee", "duedate" };
            var available = fields.Select(field => field.GetProperty("fieldId").GetString()).ToHashSet();
            if (!names.IsSubsetOf(available) || !available.Contains(JiraRestClient.DescriptionField) ||
                fields.Any(field => field.GetProperty("required").GetBoolean() &&
                !names.Contains(field.GetProperty("fieldId").GetString() ?? string.Empty) &&
                (!field.TryGetProperty("hasDefaultValue", out var hasDefault) || !hasDefault.GetBoolean())))
            {
                throw new IdentityCommandException(400, JiraRules.Invalid);
            }
            var statuses = await jiraRest.RequestAsync(uri, token, $"project/{project}/statuses", cancellation);
            var ids = statuses.EnumerateArray().Single(type => type.GetProperty("id").GetString() == row.IssueType)
                .GetProperty("statuses").EnumerateArray().Select(status => status.GetProperty("id").GetString()).ToHashSet();
            var selected = new[] { row.DoneStatus, row.HoldStatus, row.CancelStatus }.OfType<string>().ToArray();
            var permissions = await jiraRest.RequestAsync(uri, token,
                $"mypermissions?projectKey={project}&permissions=BROWSE_PROJECTS,CREATE_ISSUES,ASSIGN_ISSUES", cancellation);
            if (string.IsNullOrWhiteSpace(row.DoneStatus) || selected.Distinct().Count() != selected.Length ||
                !ids.IsSupersetOf(selected) || new[] { "BROWSE_PROJECTS", "CREATE_ISSUES", "ASSIGN_ISSUES" }.Any(permission =>
                    !permissions.GetProperty("permissions").GetProperty(permission).GetProperty("havePermission").GetBoolean()))
            {
                throw new IdentityCommandException(400, JiraRules.Invalid);
            }
        }
        catch (Exception error) when (!cancellation.IsCancellationRequested && error is
            HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new IdentityCommandException(400, JiraRules.Invalid);
        }
    }

    /// <summary>Подтверждает соответствия по рабочему email и проверяет назначаемость</summary>
    /// <param name="settings">Настройки и предлагаемые соответствия</param>
    /// <param name="token">REST-секрет</param>
    /// <param name="cancellation">Отмена проверки</param>
    private async Task VerifyJiraExecutorMappingsAsync(JiraSettingsDal settings, string token, CancellationToken cancellation)
    {
        var mappings = ReadJiraExecutors(settings);
        if (mappings.Count == 0)
        {
            return;
        }

        var ids = mappings.Keys.ToArray();
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<UserProfileDal>()}
            WHERE {EntityMapper.ColName<UserProfileDal>(x => x.UserId)} = ANY({EntityMapper.ParameterName(nameof(ids))})
              AND {EntityMapper.ColName<UserProfileDal>(x => x.IsActive)}
            """;
        var users = (await connection.QueryAsync<UserProfileDal>(query, new { ids })).ToArray();
        if (users.Length != ids.Length)
        {
            throw new IdentityCommandException(400, JiraRules.Invalid);
        }
        var address = JiraRestClient.Resolve(jiraConfiguration, settings.BaseUrl);
        try
        {
            foreach (var user in users)
            {
                var emailAddress = RequiredText(user.Email);
                var candidates = await jiraRest.RequestAsync(address, token, JiraRestClient.UserSearchPath + Uri.EscapeDataString(emailAddress), cancellation);
                var exact = candidates.EnumerateArray().Where(candidate => candidate.GetProperty("active").GetBoolean()
                    && candidate.TryGetProperty("emailAddress", out var email)
                    && string.Equals(email.GetString(), user.Email, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (exact.Length != 1 || exact[0].GetProperty("name").GetString() != mappings[user.UserId]
                    || !await jiraRest.CanAssignAsync(address, token, settings.Project, mappings[user.UserId], cancellation))
                {
                    throw new IdentityCommandException(400, JiraRules.Invalid);
                }
            }
        }
        catch (Exception error) when (error is not IdentityCommandException && !cancellation.IsCancellationRequested && IsJiraFailure(error))
        {
            throw new IdentityCommandException(400, JiraRules.Invalid);
        }
    }

    /// <summary>Привязывает шифрование REST-реквизитов к клиентскому хосту</summary>
    /// <returns>Защита секрета клиента</returns>
    private IDataProtector JiraProtector()
    {
        return protection.CreateProtector(JiraRules.Source, databaseContext.Client.Host);
    }

    /// <summary>Хеширует пароль входящей схемы Basic</summary>
    /// <param name="secret">Пароль вебхука</param>
    /// <returns>Хеш пароля</returns>
    private static byte[] HashJiraSecret(string secret)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }
}
