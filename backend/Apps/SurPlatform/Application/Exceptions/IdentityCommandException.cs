namespace Application.Exceptions;

/// <summary>
/// Ожидаемый отказ при выполнении команды Identity
/// </summary>
/// <param name="status">HTTP-совместимый статус отказа</param>
/// <param name="code">Стабильный код для клиента</param>
/// <param name="dependencyIds">Идентификаторы блокирующих связей, если применимо</param>
public sealed class IdentityCommandException(int status, string code, IReadOnlyList<Guid>? dependencyIds = null) : Exception(code)
{
    /// <summary>
    /// Статус отказа
    /// </summary>
    public int Status { get; } = status;

    /// <summary>
    /// Код бизнес-ошибки
    /// </summary>
    public string Code { get; } = code;

    /// <summary>Блокирующие связи текущего клиента</summary>
    public IReadOnlyList<Guid>? DependencyIds { get; } = dependencyIds;
}
