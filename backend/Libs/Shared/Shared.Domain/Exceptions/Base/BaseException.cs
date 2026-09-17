namespace Shared.Domain.Exceptions.Base;

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
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
    }

    ///
    protected BaseException(string code, string message, Exception innerException) : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(innerException);
        Code = code;
    }
}