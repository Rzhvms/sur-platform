namespace Application.UseCases.Organization.Dto.Request;

/// <summary>
/// Параметры административной операции оргструктуры
/// </summary>
public sealed record OwnerRequest
{
    /// <summary>
    /// Версия процесса
    /// </summary>
    public Guid Version { get; init; }

    /// <summary>
    /// Новый владелец
    /// </summary>
    public Guid OwnerId { get; init; }

    /// <summary>
    /// Явное подтверждение смены владельца
    /// </summary>
    public bool Confirmed { get; init; }
}
