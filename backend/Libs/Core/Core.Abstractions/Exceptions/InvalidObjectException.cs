using Core.Abstractions.Exceptions.Base;

namespace Core.Abstractions.Exceptions;

public class InvalidObjectException(string message) : BaseException(message)
{
    public InvalidObjectException() : this("Sorry, object is invalid") { }
}