using Shared.Domain.Exceptions.Base;

namespace Shared.Domain.Exceptions;

/// <summary>
/// Входные данные или изменение состояния не прошли проверку
/// </summary>
public class ValidationException : BaseException
{
    ///
    public ValidationException(string message) : base(CommonErrorCodes.Validation, message)
    {
    }

    ///
    public ValidationException(string code, string message) : base(code, message)
    {
    }
}