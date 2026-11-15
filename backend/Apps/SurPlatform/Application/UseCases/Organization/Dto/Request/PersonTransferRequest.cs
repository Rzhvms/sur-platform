namespace Application.UseCases.Organization.Dto.Request;

/// <summary>Переводит сотрудника в другую дирекцию с сохранением истории</summary>
public sealed record PersonTransferRequest
{
    /// <summary>Версия действующей записи сотрудника</summary>
    public Guid Version { get; init; }

    /// <summary>Целевая дирекция</summary>
    public Guid DirectorateId { get; init; }

    /// <summary>Должность на новом месте</summary>
    public string? PositionName { get; init; }
}
