using System.Data;
using Application.Constants;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Repositories.Demo.Interfaces;
using Infrastructure.Repositories.Demo.Models;

namespace Infrastructure.Repositories.Demo;

/// <summary>Создаёт связанные справочники и организационный контекст банковского проекта</summary>
internal sealed partial class BankingDemoRepository : IBankingDemoRepository
{
    /// <summary>Сохраняет оргструктуру с историей без замены существующего CEO</summary>
    /// <param name="data">Исходный набор</param>
    /// <param name="shift">Смещение дат</param>
    /// <param name="operation">Общая операция журнала</param>
    /// <param name="transaction">Общая транзакция</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task SeedOrganizationAsync(BankingDemoDataSet data, TimeSpan shift, Guid operation,
        IDbTransaction transaction, CancellationToken cancellationToken)
    {
        foreach (var source in data.Directorates)
        {
            var row = source with { CreatedAt = source.CreatedAt + shift, UpdatedAt = source.UpdatedAt + shift };
            await InsertAsync(row, transaction, cancellationToken);
            await RecordAsync(row, row.Id, row.Version, operation, transaction, cancellationToken);
        }
        foreach (var source in data.Persons)
        {
            var row = source with { CreatedAt = source.CreatedAt + shift, UpdatedAt = source.UpdatedAt + shift };
            await InsertAsync(row, transaction, cancellationToken);
            await RecordAsync(row, row.Id, row.Version, operation, transaction, cancellationToken);
        }
        foreach (var source in data.Processes)
        {
            var row = source with { CreatedAt = source.CreatedAt + shift, UpdatedAt = source.UpdatedAt + shift };
            await InsertAsync(row, transaction, cancellationToken);
            await RecordAsync(row, row.Id, row.Version, operation, transaction, cancellationToken);
        }
        foreach (var source in data.Assets)
        {
            var row = source with { CreatedAt = source.CreatedAt + shift, UpdatedAt = source.UpdatedAt + shift };
            await InsertAsync(row, transaction, cancellationToken);
            await RecordAsync(row, row.Id, row.Version, operation, transaction, cancellationToken);
        }
        var ceoQuery = $"SELECT EXISTS (SELECT FROM {EntityMapper.TbName<ResponsibilityDal>()} WHERE {EntityMapper.ColName<ResponsibilityDal>(x => x.IsActive)} AND {EntityMapper.ColName<ResponsibilityDal>(x => x.ResponsibilityType)} = {EntityMapper.ParameterName(nameof(ResponsibilityDal.ResponsibilityType))})";
        var hasCeo = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(ceoQuery,
            new { ResponsibilityType = OrganizationRoles.Ceo }, transaction, cancellationToken: cancellationToken));
        foreach (var source in data.Responsibilities)
        {
            if (source.ResponsibilityType == OrganizationRoles.Ceo && hasCeo) { continue; }
            var row = source with { CreatedAt = source.CreatedAt + shift, UpdatedAt = source.UpdatedAt + shift };
            await InsertAsync(row, transaction, cancellationToken);
            await RecordAsync(row, row.Id, row.Version, operation, transaction, cancellationToken);
        }
        foreach (var row in data.HazardGroups)
        {
            await InsertAsync(row, transaction, cancellationToken);
            await RecordAsync(row, row.Id, row.Version, operation, transaction, cancellationToken);
        }
        foreach (var row in data.Hazards)
        {
            await InsertAsync(row, transaction, cancellationToken);
            await RecordAsync(row, row.Id, row.Version, operation, transaction, cancellationToken);
        }
        foreach (var row in data.Categories)
        {
            await InsertAsync(row, transaction, cancellationToken);
            await RecordAsync(row, row.Id, row.Version, operation, transaction, cancellationToken);
        }
    }
}
