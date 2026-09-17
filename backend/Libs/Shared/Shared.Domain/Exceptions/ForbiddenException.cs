using Shared.Domain.Exceptions.Base;

namespace Shared.Domain.Exceptions;

/// <summary>
/// Пользователь аутентифицирован, но не имеет права выполнить операцию
/// </summary>
public class ForbiddenException : BaseException
{
    ///
    public ForbiddenException(string message) : base(CommonErrorCodes.AccessForbidden, message)
    {
    }

    ///
    public ForbiddenException(string code, string message) : base(code, message)
    {
    }
}