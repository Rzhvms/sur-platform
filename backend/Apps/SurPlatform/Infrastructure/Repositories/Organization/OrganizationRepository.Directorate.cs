using Application.Constants;
using Application.Exceptions;
using Application.UseCases.Organization.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;

namespace Infrastructure.Repositories.Organization;

/// <summary>Операции справочника дирекций</summary>
internal sealed partial class OrganizationRepository
{
    /// <inheritdoc/>
    public Task<DirectorateDal[]> ListDirectorateAsync(Guid after, int limit, bool? active, string? search)
    {
        return ListAsync<DirectorateDal>(after, limit, active, search, null,
            $"t.{EntityMapper.ColName<DirectorateDal>(x => x.Name)}", null);
    }

    /// <inheritdoc/>
    public Task<DirectorateDal> GetDirectorateAsync(Guid id)
    {
        return GetAsync<DirectorateDal>(id);
    }

    /// <inheritdoc/>
    public Task<DirectorateDal> CreateDirectorateAsync(Guid actor, Guid sessionId, DirectorateRequest request)
    {
        return WriteAsync(actor, sessionId, async transaction =>
        {
            var name = RequireName(request.Name);
            var query = $"""
                INSERT INTO {EntityMapper.TbName<DirectorateDal>()} ({Id}, {EntityMapper.ColName<DirectorateDal>(x => x.Name)})
                VALUES (gen_random_uuid(), {EntityMapper.ParameterName(nameof(name))})
                RETURNING *
                """;
            return await SaveAsync<DirectorateDal>(query, new { name }, actor, transaction);
        });
    }

    /// <inheritdoc/>
    public Task<DirectorateDal> UpdateDirectorateAsync(Guid actor, Guid sessionId, Guid id, DirectorateRequest request, Guid version)
    {
        return WriteAsync(actor, sessionId, async transaction =>
        {
            var row = await GetAsync<DirectorateDal>(id, transaction);
            RequireVersion(row, version);
            var name = RequireName(request.Name);
            var query = $"""
                UPDATE {EntityMapper.TbName<DirectorateDal>()}
                SET {EntityMapper.ColName<DirectorateDal>(x => x.Name)} = {EntityMapper.ParameterName(nameof(name))}, {Touch}
                WHERE {Id} = {EntityMapper.ParameterName(nameof(id))} AND {Version} = {EntityMapper.ParameterName(nameof(version))}
                RETURNING *
                """;
            return await SaveAsync<DirectorateDal>(query, new { id, version, name }, actor, transaction);
        });
    }

    /// <inheritdoc/>
    public Task<DirectorateDal> SetDirectorateStateAsync(Guid actor, Guid sessionId, Guid id, OrgStateRequest request)
    {
        return WriteAsync(actor, sessionId, async transaction =>
        {
            var row = await GetAsync<DirectorateDal>(id, transaction);
            RequireVersion(row, request.Version);
            if (!request.IsActive)
            {
                var dependenciesQuery = $"""
                    SELECT {Id} FROM {EntityMapper.TbName<OrgPersonDal>()}
                    WHERE {IsActive} AND {EntityMapper.ColName<OrgPersonDal>(x => x.DirectorateId)} = {EntityMapper.ParameterName(nameof(id))}
                    UNION ALL
                    SELECT {Id} FROM {EntityMapper.TbName<ProcessDal>()}
                    WHERE {IsActive} AND {EntityMapper.ColName<ProcessDal>(x => x.DirectorateId)} = {EntityMapper.ParameterName(nameof(id))}
                    UNION ALL
                    SELECT {Id} FROM {EntityMapper.TbName<ResponsibilityDal>()}
                    WHERE {IsActive} AND {EntityMapper.ColName<ResponsibilityDal>(x => x.DirectorateId)} = {EntityMapper.ParameterName(nameof(id))}
                    """;
                var ids = (await connection.QueryAsync<Guid>(dependenciesQuery, new { id }, transaction)).ToArray();
                if (ids.Length > 0)
                {
                    throw new IdentityCommandException(409, OrganizationErrors.Dependencies, ids);
                }
            }

            return await SetStateAsync(row, request.IsActive, actor, transaction);
        });
    }

    /// <summary>Нормализует обязательное название справочника</summary>
    /// <param name="value">Введённое название</param>
    /// <returns>Название без крайних пробелов</returns>
    /// <exception cref="IdentityCommandException">Название пустое</exception>
    private static string RequireName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new IdentityCommandException(400, OrganizationErrors.Invalid);
        }

        return value.Trim();
    }
}
