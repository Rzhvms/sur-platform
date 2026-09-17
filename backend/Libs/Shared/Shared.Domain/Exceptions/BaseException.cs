namespace Shared.Domain.Exceptions;

/// <summary>
/// Базовый тип ожидаемых исключений приложения
/// </summary>
public abstract class BaseException : Exception
{
    /// <summary>
    /// Код ошибки
    /// </summary>
    public string Code { get; }

    ///
    protected BaseException(string code, string message) : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    ///
    protected BaseException(string code, string message, Exception innerException) : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }
}