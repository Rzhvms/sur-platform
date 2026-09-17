using Shared.Domain.Exceptions;

namespace IdentityApi.Application.Exceptions;

/// <summary>
/// Токен отсутствует, поврежден или не прошел криптографическую проверку
/// </summary>
public sealed class InvalidTokenException : AuthenticationException
{
    ///
    public InvalidTokenException(string message) : base("identity.invalid_token", message)
    {
    }

    ///
    public InvalidTokenException(string message, Exception innerException) : base("identity.invalid_token", message, innerException)
    {
    }
}
