using Infrastructure.Repositories.Risks.Helpers;
using Application.Constants;
using Application.Exceptions;
using Application.UseCases.Organization.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Domain.DalModels;

namespace Infrastructure.Repositories.Organization;

/// <summary>Операции справочника объектов</summary>
internal sealed partial class OrganizationRepository
{
    /// <inheritdoc/>
    public Task<AssetDal[]> ListAssetAsync(Guid after, int limit, bool? active, string? search)
    {
        return ListAsync<AssetDal>(after, limit, active, search, null,
            $"concat_ws(' ', t.{EntityMapper.ColName<AssetDal>(x => x.Name)}, t.{EntityMapper.ColName<AssetDal>(x => x.InventoryNumber)})", null);
    }

    /// <inheritdoc/>
    public Task<AssetDal> GetAssetAsync(Guid id)
    {
        return GetAsync<AssetDal>(id);
    }

    /// <inheritdoc/>
    public Task<AssetDal> CreateAssetAsync(Guid actor, Guid sessionId, AssetRequest request)
    {
        return WriteAsync(actor, sessionId, async transaction =>
        {
            var name = RequireName(request.Name);
            var description = request.Description?.Trim();
            var inventoryNumber = request.InventoryNumber?.Trim();
            var cost = RequireCost(request.HourlyDowntimeCost);
            var query = $"""
                INSERT INTO {EntityMapper.TbName<AssetDal>()} ({Id}, {EntityMapper.ColName<AssetDal>(x => x.Name)}, {EntityMapper.ColName<AssetDal>(x => x.Description)}, {EntityMapper.ColName<AssetDal>(x => x.InventoryNumber)}, {EntityMapper.ColName<AssetDal>(x => x.HourlyDowntimeCost)})
                VALUES (gen_random_uuid(), {EntityMapper.ParameterName(nameof(name))}, {EntityMapper.ParameterName(nameof(description))}, {EntityMapper.ParameterName(nameof(inventoryNumber))}, {EntityMapper.ParameterName(nameof(cost))})
                RETURNING *
                """;
            return await SaveAsync<AssetDal>(query, new { name, description, inventoryNumber, cost }, actor, transaction);
        });
    }

    /// <inheritdoc/>
    public Task<AssetDal> UpdateAssetAsync(Guid actor, Guid sessionId, Guid id, AssetRequest request, Guid version)
    {
        return WriteAsync(actor, sessionId, async transaction =>
        {
            var row = await GetAsync<AssetDal>(id, transaction);
            RequireVersion(row, version);
            var name = RequireName(request.Name);
            var description = request.Description?.Trim();
            var inventoryNumber = request.InventoryNumber?.Trim();
            var cost = RequireCost(request.HourlyDowntimeCost);
            var query = $"""
                UPDATE {EntityMapper.TbName<AssetDal>()}
                SET {EntityMapper.ColName<AssetDal>(x => x.Name)} = {EntityMapper.ParameterName(nameof(name))},
                    {EntityMapper.ColName<AssetDal>(x => x.Description)} = {EntityMapper.ParameterName(nameof(description))},
                    {EntityMapper.ColName<AssetDal>(x => x.InventoryNumber)} = {EntityMapper.ParameterName(nameof(inventoryNumber))},
                    {EntityMapper.ColName<AssetDal>(x => x.HourlyDowntimeCost)} = {EntityMapper.ParameterName(nameof(cost))}, {Touch}
                WHERE {Id} = {EntityMapper.ParameterName(nameof(id))} AND {Version} = {EntityMapper.ParameterName(nameof(version))}
                RETURNING *
                """;
            var updated = await SaveAsync<AssetDal>(query, new { id, version, name, description, inventoryNumber, cost }, actor, transaction);
            if (row.HourlyDowntimeCost != updated.HourlyDowntimeCost)
            {
                var operationId = Guid.NewGuid();
                await BusinessHistory.RecordAsync(transaction, operationId, actor, id,
                    new { row.HourlyDowntimeCost }, new { updated.HourlyDowntimeCost }, EntityMapper.TbName<AssetDal>(false));
                await riskOrganization.RecalculateAssetAsync(transaction, actor, operationId, updated);
            }

            return updated;
        });
    }

    /// <inheritdoc/>
    public Task<AssetDal> SetAssetStateAsync(Guid actor, Guid sessionId, Guid id, OrgStateRequest request)
    {
        return WriteAsync(actor, sessionId, async transaction =>
        {
            var row = await GetAsync<AssetDal>(id, transaction);
            RequireVersion(row, request.Version);
            return await SetStateAsync(row, request.IsActive, actor, transaction);
        });
    }

    /// <summary>Проверяет стоимость часа простоя до записи</summary>
    /// <param name="value">Стоимость из запроса</param>
    /// <returns>Неотрицательная стоимость с точностью до копеек</returns>
    /// <exception cref="IdentityCommandException">Стоимость отсутствует или недопустима</exception>
    private static decimal RequireCost(decimal? value)
    {
        if (value is not decimal cost || cost < 0 || decimal.Round(cost, 2) != cost)
        {
            throw new IdentityCommandException(400, OrganizationErrors.Invalid);
        }

        return cost;
    }
}
