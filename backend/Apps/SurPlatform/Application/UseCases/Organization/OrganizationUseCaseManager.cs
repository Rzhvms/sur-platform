using Application.UseCases.Organization.Interfaces;
using Application.Models;
using Application.UseCases.Organization.Dto.Request;
using Domain.DalModels;
using Application.Ports.Repositories;
using Domain.DalModels.Organization;

namespace Application.UseCases.Organization;

/// <summary>
/// Координирует административные операции оргструктуры
/// </summary>
/// <param name="repository">Хранилище выбранного клиента</param>
internal sealed class OrganizationUseCaseManager(IOrganizationRepository repository) : IOrganizationUseCaseManager
{
    /// <inheritdoc/>
    public Task<DirectorateDal[]> ListDirectorateAsync(Guid after, int limit, bool? active, string? search)
    {
        return repository.ListDirectorateAsync(after, limit, active, search);
    }

    /// <inheritdoc/>
    public Task<DirectorateDal> GetDirectorateAsync(Guid id)
    {
        return repository.GetDirectorateAsync(id);
    }

    /// <inheritdoc/>
    public Task<DirectorateDal> CreateDirectorateAsync(Guid actor, Guid sessionId, DirectorateRequest request)
    {
        return repository.CreateDirectorateAsync(actor, sessionId, request);
    }

    /// <inheritdoc/>
    public Task<DirectorateDal> UpdateDirectorateAsync(Guid actor, Guid sessionId, Guid id, DirectorateRequest request, Guid version)
    {
        return repository.UpdateDirectorateAsync(actor, sessionId, id, request, version);
    }

    /// <inheritdoc/>
    public Task<DirectorateDal> SetDirectorateStateAsync(Guid actor, Guid sessionId, Guid id, OrgStateRequest request)
    {
        return repository.SetDirectorateStateAsync(actor, sessionId, id, request);
    }

    /// <inheritdoc/>
    public Task<OrgPersonDal[]> ListPersonAsync(Guid after, int limit, bool? active, string? search, Guid? directorateId)
    {
        return repository.ListPersonAsync(after, limit, active, search, directorateId);
    }

    /// <inheritdoc/>
    public Task<OrgPersonDal> GetPersonAsync(Guid id)
    {
        return repository.GetPersonAsync(id);
    }

    /// <inheritdoc/>
    public Task<OrgPersonDal> CreatePersonAsync(Guid actor, Guid sessionId, PersonRequest request)
    {
        return repository.CreatePersonAsync(actor, sessionId, request);
    }

    /// <inheritdoc/>
    public Task<OrgPersonDal> TransferPersonAsync(Guid actor, Guid sessionId, Guid id, PersonTransferRequest request)
    {
        return repository.TransferPersonAsync(actor, sessionId, id, request);
    }

    /// <inheritdoc/>
    public Task<OrgPersonDal> UpdatePersonAsync(Guid actor, Guid sessionId, Guid id, PersonEditRequest request)
    {
        return repository.UpdatePersonAsync(actor, sessionId, id, request);
    }

    /// <inheritdoc/>
    public Task<OrgPersonDal> SetPersonStateAsync(Guid actor, Guid sessionId, Guid id, OrgStateRequest request)
    {
        return repository.SetPersonStateAsync(actor, sessionId, id, request);
    }

    /// <inheritdoc/>
    public Task<ProcessDal[]> ListProcessAsync(Guid after, int limit, bool? active, string? search, Guid? directorateId)
    {
        return repository.ListProcessAsync(after, limit, active, search, directorateId);
    }

    /// <inheritdoc/>
    public Task<ProcessDal> GetProcessAsync(Guid id)
    {
        return repository.GetProcessAsync(id);
    }

    /// <inheritdoc/>
    public Task<ProcessDal> CreateProcessAsync(Guid actor, Guid sessionId, ProcessRequest request)
    {
        return repository.CreateProcessAsync(actor, sessionId, request);
    }

    /// <inheritdoc/>
    public Task<ProcessDal> UpdateProcessAsync(Guid actor, Guid sessionId, Guid id, ProcessEditRequest request)
    {
        return repository.UpdateProcessAsync(actor, sessionId, id, request);
    }

    /// <inheritdoc/>
    public Task<ProcessDal> SetProcessStateAsync(Guid actor, Guid sessionId, Guid id, OrgStateRequest request)
    {
        return repository.SetProcessStateAsync(actor, sessionId, id, request);
    }

    /// <inheritdoc/>
    public Task<AssetDal[]> ListAssetAsync(Guid after, int limit, bool? active, string? search)
    {
        return repository.ListAssetAsync(after, limit, active, search);
    }

    /// <inheritdoc/>
    public Task<AssetDal> GetAssetAsync(Guid id)
    {
        return repository.GetAssetAsync(id);
    }

    /// <inheritdoc/>
    public Task<AssetDal> CreateAssetAsync(Guid actor, Guid sessionId, AssetRequest request)
    {
        return repository.CreateAssetAsync(actor, sessionId, request);
    }

    /// <inheritdoc/>
    public Task<AssetDal> UpdateAssetAsync(Guid actor, Guid sessionId, Guid id, AssetRequest request, Guid version)
    {
        return repository.UpdateAssetAsync(actor, sessionId, id, request, version);
    }

    /// <inheritdoc/>
    public Task<AssetDal> SetAssetStateAsync(Guid actor, Guid sessionId, Guid id, OrgStateRequest request)
    {
        return repository.SetAssetStateAsync(actor, sessionId, id, request);
    }

    /// <inheritdoc/>
    public Task<ResponsibilityDal[]> ListResponsibilityAsync(Guid after, int limit, bool? active, string? search, Guid? directorateId, string? responsibilityType)
    {
        return repository.ListResponsibilityAsync(after, limit, active, search, directorateId, responsibilityType);
    }

    /// <inheritdoc/>
    public Task<ResponsibilityDal> GetResponsibilityAsync(Guid id)
    {
        return repository.GetResponsibilityAsync(id);
    }

    /// <inheritdoc/>
    public Task<ResponsibilityDal> CreateResponsibilityAsync(Guid actor, Guid sessionId, ResponsibilityRequest request)
    {
        return repository.CreateResponsibilityAsync(actor, sessionId, request);
    }

    /// <inheritdoc/>
    public Task<ResponsibilityDal> SetResponsibilityStateAsync(Guid actor, Guid sessionId, Guid id, OrgStateRequest request)
    {
        return repository.SetResponsibilityStateAsync(actor, sessionId, id, request);
    }

    /// <inheritdoc/>
    public Task<ProcessDal> ChangeOwnerAsync(Guid actor, Guid sessionId, Guid id, OwnerRequest request)
    {
        return repository.ChangeOwnerAsync(actor, sessionId, id, request);
    }

    /// <inheritdoc/>
    public Task<OrganizationSlot> GetSlotAsync(string responsibilityType, Guid? directorateId)
    {
        return repository.GetSlotAsync(responsibilityType, directorateId);
    }

    /// <inheritdoc/>
    public Task<OrganizationUserContext> GetUserContextAsync(Guid userId)
    {
        return repository.GetUserContextAsync(userId);
    }
}
