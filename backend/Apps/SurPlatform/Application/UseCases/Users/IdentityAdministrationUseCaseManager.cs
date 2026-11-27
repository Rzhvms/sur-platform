using System.Net;
using Application.Constants;
using Application.Exceptions;
using Application.UseCases.Users.Dto.Response;
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
/// <param name="organization">Хранилище организационных зависимостей</param>
/// <param name="access">Сервис выдачи временного доступа</param>
internal sealed class IdentityAdministrationUseCaseManager(
    IIdentityAdministrationRepository repository, IIdentityAccessService access, IOrganizationRepository organization) : IIdentityAdministrationUseCaseManager
{
    /// <inheritdoc/>
    public Task<IdentityUser[]> ListAsync(Guid after, int limit)
    {
        if (limit is < IdentityQueryLimits.MinPageSize or > IdentityQueryLimits.MaxPageSize)
        {
            throw new IdentityCommandException((int)HttpStatusCode.BadRequest, IdentityErrorCodes.InvalidQuery);
        }

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

    /// <inheritdoc/>
    public async Task<GetUserListResponse> GetUserListAsync(Guid after, int limit)
    {
        var users = await ListAsync(after, limit);
        return new GetUserListResponse
        {
            UserList = users.Select(user => new GetUserResponse
            {
                Id = user.Id, Email = user.Email ?? string.Empty, RoleId = user.RoleId, FullName = user.FullName, IsActive = user.IsActive,
                FirstName = user.FirstName, LastName = user.LastName, Patronymic = user.Patronymic
            }).ToList()
        };
    }

    /// <inheritdoc/>
    public async Task<OrganizationUserContext> GetOrganizationAsync(Guid userId)
    {
        if (await repository.FindAsync(userId) is null)
        {
            throw new IdentityCommandException((int)HttpStatusCode.NotFound, IdentityErrorCodes.UserNotFound);
        }

        return await organization.GetUserContextAsync(userId);
    }
}
