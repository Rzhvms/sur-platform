using Shared.Domain.Exceptions;

namespace IdentityApi.Domain.Common;

/// <summary>
/// Нарушение бизнес-правила доменной модели
/// </summary>
public sealed class DomainException : BaseException
{
    private const string DefaultCode = "domain.validation";

    ///
    public DomainException(string message) : base(DefaultCode, message)
    {
    }

    ///
    public DomainException(string code, string message) : base(code, message)
    {
    }

    ///
    public DomainException(string code, string message, Exception innerException) : base(code, message, innerException)
    {
    }
}