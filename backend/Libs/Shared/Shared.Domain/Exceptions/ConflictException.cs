using Shared.Domain.Exceptions.Base;

namespace Shared.Domain.Exceptions;

/// <summary>
/// Операция конфликтует с текущим состоянием данных
/// </summary>
public class ConflictException : BaseException
{
    ///
    public ConflictException(string message) : base(CommonErrorCodes.Conflict, message)
    {
    }

    ///
    public ConflictException(string code, string message) : base(code, message)
    {
    }
}