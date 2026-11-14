using System.Security.Cryptography;
using Application.Constants;
using Application.Exceptions;
using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Infrastructure.Services.Documents;

/// <summary>Закрытый потоковый доступ к объектам MinIO по серверному ключу</summary>
internal sealed class DocumentStorage : IDisposable
{
    /// <summary>Адрес оператора в формате host:port</summary>
    private const string Endpoint = "Documents:Minio:Endpoint";

    /// <summary>Учётная запись хранилища</summary>
    private const string AccessKey = "Documents:Minio:AccessKey";

    /// <summary>Секрет учётной записи хранилища</summary>
    private const string SecretKey = "Documents:Minio:SecretKey";

    /// <summary>Использовать TLS при обращении к MinIO</summary>
    private const string UseTls = "Documents:Minio:UseTls";

    /// <summary>Закрытый бакет файлов</summary>
    private const string Bucket = "Documents:Minio:Bucket";

    /// <summary>Время ожидания одного запроса к хранилищу в миллисекундах</summary>
    private const int RequestTimeout = 30000;

    /// <summary>SDK-клиент с отложенной инициализацией для развёртываний без файлового хранилища</summary>
    private readonly IMinioClient? _client;

    /// <summary>Бакет развёртывания</summary>
    private readonly string _bucket;

    /// <summary>Связывает адаптер с одним хранилищем развёртывания</summary>
    /// <param name="configuration">Настройки оператора приложения</param>
    public DocumentStorage(IConfiguration configuration)
    {
        var endpoint = configuration[Endpoint];
        var accessKey = configuration[AccessKey];
        var secret = configuration[SecretKey];
        _bucket = configuration[Bucket] ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(accessKey) &&
            !string.IsNullOrWhiteSpace(secret) && !string.IsNullOrWhiteSpace(_bucket))
        {
            _client = new MinioClient().WithEndpoint(endpoint).WithCredentials(accessKey, secret)
                .WithSSL(configuration.GetValue(UseTls, true)).WithTimeout(RequestTimeout).Build();
        }
    }

    /// <summary>Сверяет ранее загруженный объект по размеру и контрольной сумме</summary>
    /// <param name="key">Изолированный серверный ключ</param>
    /// <param name="length">Ожидаемый размер</param>
    /// <param name="checksum">Ожидаемый SHA-256</param>
    /// <param name="cancellation">Отмена чтения</param>
    /// <returns>Предыдущая запись объекта подтверждена</returns>
    internal async Task<bool> ConfirmAsync(string key, long length, string checksum, CancellationToken cancellation)
    {
        try
        {
            var stat = await Client().StatObjectAsync(new StatObjectArgs().WithBucket(_bucket).WithObject(key), cancellation);
            if (stat.Size != length)
            {
                throw new IdentityCommandException(503, DocumentRules.Unavailable);
            }

            string? actual = null;
            await ReadAsync(key, async (stream, token) =>
            {
                actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
            }, cancellation);
            if (!string.Equals(actual, checksum, StringComparison.Ordinal))
            {
                throw new IdentityCommandException(503, DocumentRules.Unavailable);
            }

            return true;
        }
        catch (ObjectNotFoundException)
        {
            return false;
        }
        catch (Exception error) when (IsTransportFailure(error, cancellation))
        {
            throw new IdentityCommandException(503, DocumentRules.Unavailable);
        }
    }

    /// <summary>Создаёт объект и подтверждает содержимое после записи</summary>
    /// <param name="key">Изолированный серверный ключ</param>
    /// <param name="source">Поток, сверенный с контрольной суммой</param>
    /// <param name="length">Размер потока</param>
    /// <param name="checksum">Ожидаемый SHA-256</param>
    /// <param name="cancellation">Отмена передачи</param>
    internal async Task SaveAsync(string key, Stream source, long length, string checksum, CancellationToken cancellation)
    {
        try
        {
            await Client().PutObjectAsync(new PutObjectArgs().WithBucket(_bucket).WithObject(key)
                .WithContentType(DocumentRules.BinaryMediaType).WithStreamData(source).WithObjectSize(length), cancellation);
        }
        catch (Exception error) when (!cancellation.IsCancellationRequested &&
            (error is MinioException || IsTransportFailure(error, cancellation)))
        {
            if (await ConfirmAsync(key, length, checksum, cancellation))
            {
                return;
            }

            throw new IdentityCommandException(503, DocumentRules.Unavailable);
        }

        if (!await ConfirmAsync(key, length, checksum, cancellation))
        {
            throw new IdentityCommandException(503, DocumentRules.Unavailable);
        }
    }

    /// <summary>Передаёт объект потоком, закрываемым SDK после вызова получателя</summary>
    /// <param name="key">Изолированный серверный ключ</param>
    /// <param name="consume">Получатель потока</param>
    /// <param name="cancellation">Отмена передачи</param>
    internal async Task ReadAsync(string key, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellation)
    {
        try
        {
            await Client().GetObjectAsync(new GetObjectArgs().WithBucket(_bucket).WithObject(key)
                .WithCallbackStream(consume), cancellation);
        }
        catch (Exception error) when (IsTransportFailure(error, cancellation))
        {
            throw new IdentityCommandException(503, DocumentRules.Unavailable);
        }
    }

    /// <summary>Отличает недоступность хранилища от отмены вызывающей операции</summary>
    /// <param name="error">Ошибка транспортного слоя SDK</param>
    /// <param name="cancellation">Токен вызывающей операции</param>
    /// <returns>Сбой подключения или истечение тайм-аута транспорта</returns>
    private static bool IsTransportFailure(Exception error, CancellationToken cancellation)
    {
        return !cancellation.IsCancellationRequested && error is (HttpRequestException or OperationCanceledException);
    }

    /// <summary>Получает настроенный SDK-клиент</summary>
    /// <returns>Клиент закрытого бакета</returns>
    /// <exception cref="IdentityCommandException">Развёртывание не настроило файловое хранилище</exception>
    private IMinioClient Client()
    {
        return _client ?? throw new IdentityCommandException(503, DocumentRules.Unavailable);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _client?.Dispose();
    }
}
