using System.Data;
using Application.UseCases.Organization.Dto.Request;

namespace Infrastructure.Repositories.Organization.Interfaces;

/// <summary>Подключает организационные зависимости к операциям Identity</summary>
internal interface IOrganizationIdentityRepository
{
    /// <summary>Формирует подзапрос действующих зон и процессов пользователя</summary>
    /// <param name="userIdExpression">Доверенная колонка или параметр из EntityMapper</param>
    /// <returns>SQL для административной карточки</returns>
    string BuildResponsibilityLabelsQuery(string userIdExpression);

    /// <summary>Читает действующие зоны и процессы для приглашения</summary>
    /// <param name="userId">Получатель доступа</param>
    /// <param name="transaction">Транзакция вызывающей операции</param>
    /// <returns>Подписи зон и процессов</returns>
    Task<string[]> ReadResponsibilitiesAsync(Guid userId, IDbTransaction transaction);

    /// <summary>Проверяет пригодность действующих назначений после изменения ролей</summary>
    /// <param name="userId">Пользователь при изменении его ролей</param>
    /// <param name="roleCode">Код при деактивации системной роли</param>
    /// <param name="transaction">Транзакция изменения ролей</param>
    Task RequireValidRolesAsync(Guid? userId, string? roleCode, IDbTransaction transaction);

    /// <summary>Сохраняет отчётную версию имени сотрудника</summary>
    /// <param name="userId">Изменённая учётная запись</param>
    /// <param name="actor">Инициатор</param>
    /// <param name="transaction">Транзакция изменения имени</param>
    Task SnapshotPersonNameAsync(Guid userId, Guid actor, IDbTransaction transaction);

    /// <summary>Создаёт сотрудника и начальные назначения для новой учётной записи</summary>
    /// <param name="userId">Новый пользователь</param>
    /// <param name="request">Данные сотрудника и назначений</param>
    /// <param name="actor">Инициатор</param>
    /// <param name="transaction">Транзакция создания пользователя</param>
    Task CreateForUserAsync(Guid userId, InitialOrganizationRequest request, Guid actor, IDbTransaction transaction);

    /// <summary>Завершает действующую запись сотрудника после проверки зависимостей</summary>
    /// <param name="userId">Деактивируемый пользователь</param>
    /// <param name="actor">Инициатор</param>
    /// <param name="transaction">Транзакция деактивации пользователя</param>
    Task DeactivateForUserAsync(Guid userId, Guid actor, IDbTransaction transaction);
}
