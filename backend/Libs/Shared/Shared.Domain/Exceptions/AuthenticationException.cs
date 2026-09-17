using Shared.Domain.Exceptions.Base;

namespace Shared.Domain.Exceptions;

/// <summary>
/// Не удалось подтвердить подлинность пользователя или его учетных данных
/// </summary>
public class AuthenticationException : BaseException
{
    ///
    public AuthenticationException(string message) : base(CommonErrorCodes.AuthenticationRequired, message)
    {
    }

    ///
    public AuthenticationException(string code, string message) : base(code, message)
    {
    }

    ///
    public AuthenticationException(string code, string message, Exception innerException) : base(code, message, innerException)
    {
    }
}