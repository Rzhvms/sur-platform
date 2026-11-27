using Core.Abstractions.Exceptions.Base;

namespace Application.Exceptions;

/// <summary>
/// Ожидаемый отказ при выполнении команды Identity
/// </summary>
public sealed class IdentityCommandException(int status, string code, IReadOnlyList<Guid>? dependencyIds = null) : BaseException(code)
{
    /// <summary>
    /// Статус отказа
    /// </summary>
    public int Status { get; } = status;

    /// <summary>
    /// Код бизнес-ошибки
    /// </summary>
    public string Code { get; } = code;

    /// <summary>
    /// Блокирующие связи текущего клиента
    /// </summary>
    public IReadOnlyList<Guid>? DependencyIds { get; } = dependencyIds;
}