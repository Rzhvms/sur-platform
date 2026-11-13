using Application.Constants;
using Application.Exceptions;
using Application.UseCases.Organization.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Domain.DalModels;

namespace Infrastructure.Repositories.Organization;

/// <summary>Операции справочника процессов</summary>
internal sealed partial class OrganizationRepository
{
    /// <inheritdoc/>
    public Task<ProcessDal[]> ListProcessAsync(Guid after, int limit, bool? active, string? search, Guid? directorateId)
    {
        return ListAsync<ProcessDal>(after, limit, active, search, directorateId,
            $"t.{EntityMapper.ColName<ProcessDal>(x => x.Name)}", EntityMapper.ColName<ProcessDal>(x => x.DirectorateId));
    }

    /// <inheritdoc/>
    public Task<ProcessDal> GetProcessAsync(Guid id)
    {
        return GetAsync<ProcessDal>(id);
    }

    /// <inheritdoc/>
    public Task<ProcessDal> CreateProcessAsync(Guid actor, Guid sessionId, ProcessRequest request)
    {
        return WriteAsync(actor, sessionId, async transaction =>
        {
            var name = RequireName(request.Name);
            var directorateId = request.DirectorateId;
            var ownerId = request.OwnerId;
            var description = request.Description?.Trim();
            await RequireOwnerAsync(ownerId, directorateId, transaction);
            var query = $"""
                INSERT INTO {EntityMapper.TbName<ProcessDal>()} ({Id}, {EntityMapper.ColName<ProcessDal>(x => x.Name)}, {EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)}, {EntityMapper.ColName<ProcessDal>(x => x.OwnerId)}, {EntityMapper.ColName<ProcessDal>(x => x.Description)})
                VALUES (gen_random_uuid(), {EntityMapper.ParameterName(nameof(name))}, {EntityMapper.ParameterName(nameof(directorateId))}, {EntityMapper.ParameterName(nameof(ownerId))}, {EntityMapper.ParameterName(nameof(description))})
                RETURNING *
                """;
            return await SaveAsync<ProcessDal>(query, new { name, directorateId, ownerId, description }, actor, transaction);
        });
    }

    /// <inheritdoc/>
    public Task<ProcessDal> UpdateProcessAsync(Guid actor, Guid sessionId, Guid id, ProcessEditRequest request)
    {
        return WriteAsync(actor, sessionId, async transaction =>
        {
            var version = request.Version;
            var row = await GetAsync<ProcessDal>(id, transaction);
            RequireVersion(row, version);
            if (row.IsActive)
            {
                await RequireOwnerAsync(row.OwnerId, row.DirectorateId, transaction);
            }

            var name = RequireName(request.Name);
            var description = request.Description?.Trim();
            var query = $"""
                UPDATE {EntityMapper.TbName<ProcessDal>()}
                SET {EntityMapper.ColName<ProcessDal>(x => x.Name)} = {EntityMapper.ParameterName(nameof(name))},
                    {EntityMapper.ColName<ProcessDal>(x => x.Description)} = {EntityMapper.ParameterName(nameof(description))}, {Touch}
                WHERE {Id} = {EntityMapper.ParameterName(nameof(id))} AND {Version} = {EntityMapper.ParameterName(nameof(version))}
                RETURNING *
                """;
            return await SaveAsync<ProcessDal>(query, new { id, version, name, description }, actor, transaction);
        });
    }

    /// <inheritdoc/>
    public Task<ProcessDal> SetProcessStateAsync(Guid actor, Guid sessionId, Guid id, OrgStateRequest request)
    {
        return WriteAsync(actor, sessionId, async transaction =>
        {
            var row = await GetAsync<ProcessDal>(id, transaction);
            RequireVersion(row, request.Version);
            if (request.IsActive)
            {
                await RequireOwnerAsync(row.OwnerId, row.DirectorateId, transaction);
            }

            return await SetStateAsync(row, request.IsActive, actor, transaction);
        });
    }

    /// <inheritdoc/>
    public Task<ProcessDal> ChangeOwnerAsync(Guid actor, Guid sessionId, Guid id, OwnerRequest request)
    {
        return WriteAsync(actor, sessionId, async transaction =>
        {
            if (!request.Confirmed)
            {
                throw new IdentityCommandException(409, OrganizationErrors.ConfirmOwner);
            }

            var process = await GetAsync<ProcessDal>(id, transaction);
            var version = request.Version;
            var ownerId = request.OwnerId;
            RequireVersion(process, version);
            await RequireOwnerAsync(ownerId, process.DirectorateId, transaction);
            if (process.OwnerId == ownerId)
            {
                return process;
            }

            var query = $"""
                UPDATE {EntityMapper.TbName<ProcessDal>()}
                SET {EntityMapper.ColName<ProcessDal>(x => x.OwnerId)} = {EntityMapper.ParameterName(nameof(ownerId))}, {Touch}
                WHERE {Id} = {EntityMapper.ParameterName(nameof(id))} AND {Version} = {EntityMapper.ParameterName(nameof(version))}
                RETURNING *
                """;
            return await SaveAsync<ProcessDal>(query, new { id, version, ownerId }, actor, transaction);
        });
    }

    /// <summary>Проверяет пригодность владельца для указанной дирекции</summary>
    /// <param name="ownerId">Выбранный сотрудник</param>
    /// <param name="directorateId">Дирекция процесса</param>
    /// <param name="transaction">Транзакция изменения процесса</param>
    /// <exception cref="IdentityCommandException">Сотрудник не может владеть процессом</exception>
    private async Task RequireOwnerAsync(Guid ownerId, Guid directorateId, System.Data.IDbTransaction transaction)
    {
        await RequireDirectorateAsync(directorateId, transaction);
        var person = await GetAsync<OrgPersonDal>(ownerId, transaction);
        if (!person.IsActive || person.DirectorateId != directorateId)
        {
            throw new IdentityCommandException(409, OrganizationErrors.Ineligible);
        }

        await RequireUserRoleAsync(person.UserId, OrganizationRoles.Owner, transaction);
    }
}
