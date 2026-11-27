using Application.UseCases.Users.Interfaces;
using Application.Models;
using Application.Ports.Repositories;
using Application.Services.Authentication.Interfaces;
using Application.UseCases.Users.Dto.Request;

namespace Application.UseCases.Users;

/// <summary>
/// Координирует административные запросы пользователей и ролей
/// </summary>
/// <param name="repository">Хранилище выбранного клиента</param>
/// <param name="access">Сервис выдачи временного доступа</param>
internal sealed class IdentityAdministrationUseCaseManager(
    IIdentityAdministrationRepository repository, IIdentityAccessService access) : IIdentityAdministrationUseCaseManager
{
    /// <inheritdoc/>
    public Task<IdentityUser[]> ListAsync(Guid after, int limit)
    {
        return repository.ListAsync(after, limit);
    }

    /// <inheritdoc/>
    public Task<IdentityUser?> FindAsync(Guid userId)
    {
        return repository.FindAsync(userId);
    }

    /// <inheritdoc/>
    public Task<IdentityRole[]> RolesAsync()
    {
        return repository.RolesAsync();
    }

    /// <inheritdoc/>
    public Task<IdentityRight[]> RightsAsync()
    {
        return repository.RightsAsync();
    }

    /// <inheritdoc/>
    public Task SynchronizeCatalogAsync(Guid actor, Guid sessionId)
    {
        return repository.SynchronizeCatalogAsync(actor, sessionId);
    }

    /// <inheritdoc/>
    public Task<IdentityRole> SaveRoleAsync(Guid actor, Guid sessionId, Guid roleId, UpdateIdentityRoleRequest request)
    {
        return repository.SaveRoleAsync(actor, sessionId, roleId, request);
    }

    /// <inheritdoc/>
    public Task<IdentityUser> CreateAsync(Guid actor, Guid sessionId, CreateIdentityUserRequest request)
    {
        return access.CreateAsync(actor, sessionId, request);
    }

    /// <inheritdoc/>
    public Task<IdentityUser> UpdateAsync(Guid actor, Guid sessionId, Guid userId, UpdateIdentityUserRequest request)
    {
        return repository.UpdateAsync(actor, sessionId, userId, request);
    }

    /// <inheritdoc/>
    public Task<IdentityUser> ReissueAsync(Guid actor, Guid sessionId, Guid userId, ActivateIdentityUserRequest? activation, Guid version, string temporaryPassword)
    {
        return access.ReissueAsync(actor, sessionId, userId, activation, version, temporaryPassword);
    }

    /// <inheritdoc/>
    public Task<IdentityUser> DeactivateAsync(Guid actor, Guid sessionId, Guid userId, Guid version)
    {
        return repository.DeactivateAsync(actor, sessionId, userId, version);
    }
}
