using System.Data;
using Application.Constants;
using Application.Exceptions;
using Application.Models;
using Application.UseCases.Organization.Dto.Request;
using Core.Database.DapperExtensions.EntityMapper;
using Dapper;
using Domain.DalModels;
using Infrastructure.Repositories.Identity;
using Npgsql;

namespace Infrastructure.Repositories.Organization;

/// <summary>Организационные назначения и сохранение истории замены</summary>
internal sealed partial class OrganizationRepository
{
    /// <summary>Код ответственности в запросах справочника</summary>
    private static readonly string ResponsibilityType = EntityMapper.ColName<ResponsibilityDal>(x => x.ResponsibilityType);

    /// <summary>Зона ответственности сотрудника</summary>
    private static readonly string ResponsibilityDirectorate = EntityMapper.ColName<ResponsibilityDal>(x => x.DirectorateId);

    /// <inheritdoc/>
    public Task<ResponsibilityDal[]> ListResponsibilityAsync(Guid after, int limit, bool? active, string? search, Guid? directorateId, string? responsibilityType)
    {
        if (responsibilityType is not null)
        {
            ValidateResponsibilityType(responsibilityType);
        }

        return ListAsync<ResponsibilityDal>(after, limit, active, search, directorateId,
            $"t.{ResponsibilityType}", ResponsibilityDirectorate,
            exactColumn: ResponsibilityType, exactValue: responsibilityType);
    }

    /// <inheritdoc/>
    public Task<ResponsibilityDal> GetResponsibilityAsync(Guid id)
    {
        return GetAsync<ResponsibilityDal>(id);
    }

    /// <inheritdoc/>
    public async Task<OrganizationSlot> GetSlotAsync(string responsibilityType, Guid? directorateId)
    {
        ValidateSlot(responsibilityType, directorateId);
        using var transaction = connection.BeginTransaction();
        await AdministrationTransaction.LockAsync(connection, transaction);
        if (directorateId is Guid directorate)
        {
            await RequireDirectorateAsync(directorate, transaction);
        }

        var slot = await ReadSlotAsync(responsibilityType, directorateId, transaction);
        var observedAt = await ReadDatabaseClockAsync(transaction);
        transaction.Commit();
        return new OrganizationSlot(new DateTimeOffset(observedAt, TimeSpan.Zero), slot?.Id);
    }

    /// <inheritdoc/>
    public Task<ResponsibilityDal> CreateResponsibilityAsync(Guid actor, Guid sessionId, ResponsibilityRequest request)
    {
        return WriteAsync(actor, sessionId, async transaction =>
        {
            ValidateSlot(request.ResponsibilityType, request.DirectorateId);
            var current = await ReadSlotAsync(request.ResponsibilityType, request.DirectorateId, transaction);
            if (request.PreviousId is Guid previousId)
            {
                if (current is null || current.Id != previousId || current.Version != request.PreviousVersion)
                {
                    throw new IdentityCommandException(409, OrganizationErrors.StaleVersion);
                }

                await SetStateAsync(current, false, actor, transaction);
            }
            else if (request.PreviousVersion is not null || current is not null)
            {
                throw new IdentityCommandException(409, OrganizationErrors.SlotOccupied);
            }

            var person = await ResolvePersonAsync(request, actor, transaction);
            return await InsertResponsibilityAsync(person, request.ResponsibilityType, request.DirectorateId, actor, transaction);
        });
    }

    /// <inheritdoc/>
    public Task<ResponsibilityDal> SetResponsibilityStateAsync(Guid actor, Guid sessionId, Guid id, OrgStateRequest request)
    {
        return WriteAsync(actor, sessionId, async transaction =>
        {
            var row = await GetAsync<ResponsibilityDal>(id, transaction);
            RequireVersion(row, request.Version);
            if (request.IsActive)
            {
                throw new IdentityCommandException(409, OrganizationErrors.ClosedAssignment);
            }

            return await SetStateAsync(row, false, actor, transaction);
        });
    }

    /// <summary>Создаёт сотрудника и выбранные назначения в транзакции выдачи доступа</summary>
    /// <param name="userId">Только что созданная учётная запись</param>
    /// <param name="request">Поля сотрудника и выбранные позиции</param>
    /// <param name="actor">Администратор</param>
    /// <param name="transaction">Транзакция создания пользователя</param>
    public async Task CreateForUserAsync(Guid userId, InitialOrganizationRequest request, Guid actor, IDbTransaction transaction)
    {
        if (request.Responsibilities is null || request.Responsibilities.Any(slot => slot is null) ||
            (request.DirectorateId is null && !request.Responsibilities.Any(slot => slot.ResponsibilityType == OrganizationRoles.Ceo)))
        {
            throw new IdentityCommandException(400, OrganizationErrors.Invalid);
        }

        var selected = new HashSet<(string Type, Guid? DirectorateId)>();
        foreach (var slot in request.Responsibilities)
        {
            if (!selected.Add((slot.ResponsibilityType, slot.DirectorateId)))
            {
                throw new IdentityCommandException(400, OrganizationErrors.Invalid);
            }

            await RequireFreeSlotAsync(slot.ResponsibilityType, slot.DirectorateId, slot.ObservedAt, transaction);
        }

        var person = await InsertPersonAsync(userId, request.DirectorateId, request.PositionName, actor, transaction);
        foreach (var slot in request.Responsibilities)
        {
            await InsertResponsibilityAsync(person, slot.ResponsibilityType, slot.DirectorateId, actor, transaction);
        }
    }

    /// <summary>Проверяет известность кода для фильтра истории</summary>
    /// <param name="type">Точный код ответственности</param>
    private static void ValidateResponsibilityType(string type)
    {
        if (type is not (OrganizationRoles.Ceo or OrganizationRoles.Director or OrganizationRoles.RiskSpecialist))
        {
            throw new IdentityCommandException(400, OrganizationErrors.Invalid);
        }
    }

    /// <summary>Проверяет форму области ответственности</summary>
    /// <param name="type">Код ответственности</param>
    /// <param name="directorateId">Дирекция для локального назначения</param>
    private static void ValidateSlot(string type, Guid? directorateId)
    {
        var valid = type switch
        {
            OrganizationRoles.Ceo => directorateId is null,
            OrganizationRoles.Director or OrganizationRoles.RiskSpecialist => directorateId is Guid id && id != Guid.Empty,
            _ => false
        };
        if (!valid)
        {
            throw new IdentityCommandException(400, OrganizationErrors.Invalid);
        }
    }

    /// <summary>Читает время наблюдения из той же базы, что хранит назначения</summary>
    /// <param name="transaction">Транзакция наблюдения или назначения</param>
    /// <returns>Момент времени UTC</returns>
    private Task<DateTime> ReadDatabaseClockAsync(IDbTransaction transaction)
    {
        var clockQuery = "SELECT clock_timestamp()";
        return connection.QuerySingleAsync<DateTime>(clockQuery, transaction: transaction);
    }

    /// <summary>Читает текущее действующее назначение позиции</summary>
    /// <param name="type">Код ответственности</param>
    /// <param name="directorateId">Дирекция или null для CEO</param>
    /// <param name="transaction">Транзакция текущей операции</param>
    /// <returns>Действующая запись или null</returns>
    private Task<ResponsibilityDal?> ReadSlotAsync(string type, Guid? directorateId, IDbTransaction transaction)
    {
        var query = $"""
            SELECT * FROM {EntityMapper.TbName<ResponsibilityDal>()}
            WHERE {IsActive} AND {ResponsibilityType} = {EntityMapper.ParameterName(nameof(type))}
                AND {ResponsibilityDirectorate} IS NOT DISTINCT FROM CAST({EntityMapper.ParameterName(nameof(directorateId))} AS uuid)
            """;
        return connection.QuerySingleOrDefaultAsync<ResponsibilityDal>(query, new { type, directorateId }, transaction);
    }

    /// <summary>Различает ранее занятую позицию и изменение после чтения формы</summary>
    /// <param name="type">Код ответственности</param>
    /// <param name="directorateId">Дирекция или null для CEO</param>
    /// <param name="observedAt">Момент проверки позиции в базе</param>
    /// <param name="transaction">Транзакция текущей операции</param>
    private async Task RequireFreeSlotAsync(string type, Guid? directorateId, DateTimeOffset? observedAt, IDbTransaction transaction)
    {
        ValidateSlot(type, directorateId);
        if (observedAt is { } observation)
        {
            var now = await ReadDatabaseClockAsync(transaction);
            if (observation == default || observation.UtcDateTime > now)
            {
                throw new IdentityCommandException(400, OrganizationErrors.Invalid);
            }
        }

        var current = await ReadSlotAsync(type, directorateId, transaction);
        if (current is not null)
        {
            var code = observedAt is { } instant && current.CreatedAt > instant.UtcDateTime
                ? OrganizationErrors.SlotChanged
                : OrganizationErrors.SlotOccupied;
            throw new IdentityCommandException(409, code, [current.Id]);
        }
    }

    /// <summary>Разрешает сотрудника или создаёт запись CEO без дирекции</summary>
    /// <param name="request">Выбор пользователя или сотрудника</param>
    /// <param name="actor">Администратор</param>
    /// <param name="transaction">Транзакция назначения</param>
    /// <returns>Выбранный сотрудник</returns>
    private async Task<OrgPersonDal> ResolvePersonAsync(ResponsibilityRequest request, Guid actor, IDbTransaction transaction)
    {
        if (request.PersonId is Guid personId && request.UserId is null)
        {
            return await GetAsync<OrgPersonDal>(personId, transaction);
        }

        if (request.PersonId is null && request.UserId is Guid userId && request.ResponsibilityType == OrganizationRoles.Ceo)
        {
            await RequireUserRoleAsync(userId, OrganizationRoles.Ceo, transaction);
            return await FindActivePersonAsync(userId, transaction)
                ?? await InsertPersonAsync(userId, null, request.PositionName, actor, transaction);
        }

        throw new IdentityCommandException(400, OrganizationErrors.Invalid);
    }

    /// <summary>Создаёт назначение после проверки роли и области ответственности</summary>
    /// <param name="person">Сотрудник назначения</param>
    /// <param name="type">Требуемая роль и тип ответственности</param>
    /// <param name="directorateId">Дирекция назначения</param>
    /// <param name="actor">Администратор</param>
    /// <param name="transaction">Транзакция назначения</param>
    /// <returns>Действующая запись ответственности</returns>
    private async Task<ResponsibilityDal> InsertResponsibilityAsync(OrgPersonDal person, string type, Guid? directorateId,
        Guid actor, IDbTransaction transaction)
    {
        if (!person.IsActive || (type == OrganizationRoles.Director && person.DirectorateId != directorateId))
        {
            throw new IdentityCommandException(409, OrganizationErrors.Ineligible);
        }

        await RequireUserRoleAsync(person.UserId, type, transaction);
        if (directorateId is Guid directorate)
        {
            await RequireDirectorateAsync(directorate, transaction);
        }

        var personId = person.Id;
        var query = $"""
            INSERT INTO {EntityMapper.TbName<ResponsibilityDal>()}
                ({Id}, {ResponsibilityType}, {ResponsibilityDirectorate}, {EntityMapper.ColName<ResponsibilityDal>(x => x.PersonId)})
            VALUES (gen_random_uuid(), {EntityMapper.ParameterName(nameof(type))},
                {EntityMapper.ParameterName(nameof(directorateId))}, {EntityMapper.ParameterName(nameof(personId))})
            RETURNING *
            """;
        try
        {
            return await SaveAsync<ResponsibilityDal>(query, new { personId, type, directorateId }, actor, transaction);
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new IdentityCommandException(409, OrganizationErrors.SlotChanged);
        }
    }
}
