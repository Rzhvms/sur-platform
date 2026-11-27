namespace Application.UseCases.Organization.Dto.Request;

/// <summary>
/// Параметры административной операции оргструктуры
/// </summary>
public sealed record OrgStateRequest
{
    /// <summary>
    /// Версия записи
    /// </summary>
    public Guid Version { get; init; }

    /// <summary>
    /// Требуемое состояние
    /// </summary>
    public required bool IsActive { get; init; }

    /// <summary>
    /// Назначить CEO при активации сотрудника без дирекции
    /// </summary>
    public bool AssignCeo { get; init; }
}
