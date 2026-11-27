using Application.UseCases.Jira.Interfaces;
using System.Text.Json;
using Application.Models;
using Application.UseCases.Jira.Dto.Request;
using Domain.DalModels;

using Application.Ports.Repositories;
using Domain.DalModels.Jira;

namespace Application.UseCases.Jira;

/// <summary>Прикладные команды Jira</summary>
/// <param name="repository">Клиентское хранилище интеграции</param>
internal sealed class JiraUseCaseManager(IJiraRepository repository) : IJiraUseCaseManager
{
    /// <inheritdoc/>
    public Task<JsonElement> DiscoverJiraAsync(RiskActor actor, JiraDiscoveryRequest request, CancellationToken cancellation)
    {
        return repository.DiscoverJiraAsync(actor, request, cancellation);
    }

    /// <inheritdoc/>
    public Task<JiraSettingsDal?> GetJiraAsync(RiskActor actor)
    {
        return repository.GetJiraAsync(actor);
    }

    /// <inheritdoc/>
    public Task<JiraAdministrationView?> GetJiraAdministrationAsync(RiskActor actor)
    {
        return repository.GetJiraAdministrationAsync(actor);
    }

    /// <inheritdoc/>
    public Task<JiraSettingsDal> SaveJiraAsync(RiskActor actor, JiraSettingsRequest request, CancellationToken cancellation)
    {
        return repository.SaveJiraAsync(actor, request, cancellation);
    }

    /// <inheritdoc/>
    public Task<JiraErrorDal[]> ListJiraErrorsAsync(RiskActor actor, int offset, int limit)
    {
        return repository.ListJiraErrorsAsync(actor, offset, limit);
    }

    /// <inheritdoc/>
    public Task<JiraTreatmentView> ResolveJiraAsync(RiskActor actor, Guid id, JiraResolveRequest request, CancellationToken cancellation)
    {
        return repository.ResolveJiraAsync(actor, id, request, cancellation);
    }

    /// <inheritdoc/>
    public Task AuthorizeJiraWebhookAsync(string secret)
    {
        return repository.AuthorizeJiraWebhookAsync(secret);
    }

    /// <inheritdoc/>
    public Task<Guid> AcceptJiraSignalAsync(string secret, string issueId)
    {
        return repository.AcceptJiraSignalAsync(secret, issueId);
    }
}
