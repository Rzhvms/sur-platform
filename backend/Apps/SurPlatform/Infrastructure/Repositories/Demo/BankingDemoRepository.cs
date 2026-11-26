using System.Data;
using System.Text.Json;
using Application.Constants;
using Application.Helpers.Encryption;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Repositories.Demo.Interfaces;
using Infrastructure.Repositories.Demo.Models;
using Infrastructure.Repositories.Identity;
using Infrastructure.Repositories.Risks.Helpers;

namespace Infrastructure.Repositories.Demo;

/// <summary>Атомарно устанавливает первоначальный банковский демонстрационный набор</summary>
/// <param name="connection">Открытое подключение доверенного клиентского контекста</param>
internal sealed partial class BankingDemoRepository(IDbConnection connection) : IBankingDemoRepository
{
    /// <summary>Версия однократного набора, не являющаяся версией миграции</summary>
    private const int SeedId = 1;

    /// <summary>Источник записей первоначального демонстрационного заполнения</summary>
    private const string SeedSource = "DEMO_SEED";

    /// <summary>Причина отказа при отсутствии системного каталога</summary>
    private const string MissingRole = "Для демонстрационного набора требуется действующая системная роль";

    /// <inheritdoc/>
    public async Task<bool> SeedAsync(BankingDemoDataSet data, string? password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var transaction = connection.BeginTransaction();
        // Тот же lock используют изменение ролей, назначения и деактивация пользователей.
        await AdministrationTransaction.LockAsync(connection, transaction);
        var markerQuery = $"SELECT EXISTS (SELECT FROM {EntityMapper.TbName<DemoDataSeedDal>()} WHERE {EntityMapper.ColName<DemoDataSeedDal>(x => x.Id)} = {EntityMapper.ParameterName(nameof(SeedId))})";
        if (await connection.ExecuteScalarAsync<bool>(new CommandDefinition(markerQuery, new { SeedId }, transaction, cancellationToken: cancellationToken)))
        {
            return false;
        }

        var timeShift = DateTime.UtcNow - data.ReferenceDate;
        var operation = Guid.NewGuid();
        await SeedUsersAsync(data, password, timeShift, transaction, cancellationToken);
        await SeedOrganizationAsync(data, timeShift, operation, transaction, cancellationToken);
        await SeedRegistryAsync(data, timeShift, operation, transaction, cancellationToken);
        await SeedTreatmentsAsync(data, timeShift, operation, transaction, cancellationToken);
        await InsertAsync(new DemoDataSeedDal { Id = SeedId, AppliedAt = DateTime.UtcNow }, transaction, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        transaction.Commit();
        return true;
    }

    /// <summary>Создаёт только новые учётные записи, не изменяя существующую матрицу прав</summary>
    /// <param name="data">Исходный набор</param>
    /// <param name="password">Необязательный пароль новых пользователей</param>
    /// <param name="shift">Смещение времён относительно первого запуска</param>
    /// <param name="transaction">Общая транзакция</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task SeedUsersAsync(BankingDemoDataSet data, string? password, TimeSpan shift,
        IDbTransaction transaction, CancellationToken cancellationToken)
    {
        var rolesQuery = $"SELECT {EntityMapper.ColName<RoleDal>(x => x.Id)}, {EntityMapper.ColName<RoleDal>(x => x.Code)} FROM {EntityMapper.TbName<RoleDal>()} WHERE {EntityMapper.ColName<RoleDal>(x => x.IsActive)} AND {EntityMapper.ColName<RoleDal>(x => x.Code)} IS NOT NULL";
        var roles = (await connection.QueryAsync<RoleDal>(new CommandDefinition(rolesQuery, transaction: transaction, cancellationToken: cancellationToken))).ToArray();
        foreach (var user in data.Users)
        {
            var role = roles.SingleOrDefault(item => item.Code == user.RoleCode) ?? throw new InvalidOperationException(MissingRole);
            var profile = user.Profile with { CreatedAt = user.Profile.CreatedAt + shift, UpdatedAt = user.Profile.UpdatedAt + shift };
            await InsertAsync(profile, transaction, cancellationToken);
            await InsertAsync(new UserRoleDal
            {
                UserId = profile.UserId, RoleId = role.Id, IsActive = true,
                UpdatedBy = IdentityAccounts.SystemUserId, UpdatedAt = profile.UpdatedAt
            }, transaction, cancellationToken);
            if (!string.IsNullOrEmpty(password))
            {
                await InsertAsync(new UserCredentialDal
                {
                    Id = profile.UserId, UserId = profile.UserId, PasswordChangedAt = DateTime.UtcNow,
                    Password = PasswordEncryptionHelper.HashPassword(password, PasswordEncryptionHelper.GenerateSalt())
                }, transaction, cancellationToken);
            }
        }
    }

    /// <summary>Сохраняет снимок отображаемых атрибутов и событие без секретов</summary>
    /// <typeparam name="T">Тип справочной или организационной записи</typeparam>
    /// <param name="row">Сохраняемая запись</param>
    /// <param name="id">Идентификатор сущности</param>
    /// <param name="version">Идентификатор снимка</param>
    /// <param name="operation">Общая операция заполнения</param>
    /// <param name="transaction">Общая транзакция</param>
    /// <param name="cancellationToken">Отмена подготовки</param>
    private async Task RecordAsync<T>(T row, Guid id, Guid version, Guid operation,
        IDbTransaction transaction, CancellationToken cancellationToken) where T : class
    {
        var query = $"""
            INSERT INTO {EntityMapper.TbName<OrgHistoryDal>()}
                ({EntityMapper.ColName<OrgHistoryDal>(x => x.Id)}, {EntityMapper.ColName<OrgHistoryDal>(x => x.EntityId)},
                 {EntityMapper.ColName<OrgHistoryDal>(x => x.EntityType)}, {EntityMapper.ColName<OrgHistoryDal>(x => x.ActorId)},
                 {EntityMapper.ColName<OrgHistoryDal>(x => x.Snapshot)})
            VALUES ({EntityMapper.ParameterName(nameof(version))}, {EntityMapper.ParameterName(nameof(id))},
                {EntityMapper.ParameterName(nameof(OrgHistoryDal.EntityType))}, {EntityMapper.ParameterName(nameof(OrgHistoryDal.ActorId))},
                CAST({EntityMapper.ParameterName(nameof(OrgHistoryDal.Snapshot))} AS jsonb))
            """;
        await connection.ExecuteAsync(new CommandDefinition(query,
            new { version, id, EntityType = EntityMapper.TbName<T>(false), ActorId = IdentityAccounts.SystemUserId, Snapshot = JsonSerializer.Serialize(row) },
            transaction, cancellationToken: cancellationToken));
        await BusinessHistory.RecordAsync<T>(transaction, operation, IdentityAccounts.SystemUserId,
            id, null, row, system: true, automaticSource: SeedSource);
    }
}
