using Shared.Domain.Exceptions.Base;

namespace Shared.Domain.Exceptions;

/// <summary>
/// Запрошенная сущность не найдена
/// </summary>
public class EntityNotFoundException : BaseException
{
    ///
    public EntityNotFoundException(string message) : base(CommonErrorCodes.EntityNotFound, message)
    {
    }

    ///
    public EntityNotFoundException(string code, string message) : base(code, message)
    {
    }
}