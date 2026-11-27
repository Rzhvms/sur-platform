namespace Infrastructure.Services.Documents.Interfaces;

/// <summary>
/// Проверка, сохранение и чтение файлов клиентских заданий
/// </summary>
public interface IDocumentStorage : IDisposable
{
    /// <summary>Сверяет ранее загруженный объект по размеру и контрольной сумме</summary>
    /// <param name="key">Изолированный серверный ключ</param>
    /// <param name="length">Ожидаемый размер</param>
    /// <param name="checksum">Ожидаемый SHA-256</param>
    /// <param name="cancellation">Отмена чтения</param>
    /// <returns>Предыдущая запись объекта подтверждена</returns>
    Task<bool> ConfirmAsync(string key, long length, string checksum, CancellationToken cancellation);

    /// <summary>Создаёт объект и подтверждает содержимое после записи</summary>
    /// <param name="key">Изолированный серверный ключ</param>
    /// <param name="source">Поток, сверенный с контрольной суммой</param>
    /// <param name="length">Размер потока</param>
    /// <param name="checksum">Ожидаемый SHA-256</param>
    /// <param name="cancellation">Отмена передачи</param>
    Task SaveAsync(string key, Stream source, long length, string checksum, CancellationToken cancellation);

    /// <summary>Передаёт объект потоком, закрываемым SDK после вызова получателя</summary>
    /// <param name="key">Изолированный серверный ключ</param>
    /// <param name="consume">Получатель потока</param>
    /// <param name="cancellation">Отмена передачи</param>
    Task ReadAsync(string key, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellation);
}
