using Infrastructure.Services.Documents.Interfaces;
using System.Security.Cryptography;
using Application.Constants;
using Application.Exceptions;
using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Infrastructure.Services.Documents;

/// <summary>Закрытый потоковый доступ к файлам MinIO или локальной папке развёртывания</summary>
internal sealed class DocumentStorage : IDocumentStorage
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

    /// <summary>Локальная папка файлов без MinIO</summary>
    private const string LocalDirectory = "Documents:Storage:LocalDirectory";

    /// <summary>Папка по умолчанию для развёртываний без файлового хранилища</summary>
    private const string DefaultLocalDirectory = "sur-documents";

    /// <summary>Время ожидания одного запроса к хранилищу в миллисекундах</summary>
    private const int RequestTimeout = 30000;

    /// <summary>SDK-клиент с отложенной инициализацией для развёртываний без MinIO</summary>
    private readonly IMinioClient? _client;

    /// <summary>Бакет развёртывания</summary>
    private readonly string _bucket;

    /// <summary>Локальная папка, когда MinIO не настроено</summary>
    private readonly string? _localDirectory;

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
        else
        {
            var local = configuration[LocalDirectory];
            _localDirectory = string.IsNullOrWhiteSpace(local) ? DefaultLocalDirectory : local;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ConfirmAsync(string key, long length, string checksum, CancellationToken cancellation)
    {
        if (_client is null)
        {
            return await ConfirmLocalAsync(key, length, checksum, cancellation);
        }

        try
        {
            var stat = await _client.StatObjectAsync(new StatObjectArgs().WithBucket(_bucket).WithObject(key), cancellation);
            if (stat.Size != length)
            {
                throw new IdentityCommandException(503, DocumentRules.Unavailable);
            }

            string? actual = null;
            await ReadRemoteAsync(key, async (stream, token) =>
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

    /// <inheritdoc/>
    public async Task SaveAsync(string key, Stream source, long length, string checksum, CancellationToken cancellation)
    {
        if (_client is null)
        {
            await SaveLocalAsync(key, source, cancellation);
        }
        else
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
        }

        if (!await ConfirmAsync(key, length, checksum, cancellation))
        {
            throw new IdentityCommandException(503, DocumentRules.Unavailable);
        }
    }

    /// <inheritdoc/>
    public async Task ReadAsync(string key, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellation)
    {
        if (_client is null)
        {
            await using var file = File.OpenRead(LocalPath(key));
            await consume(file, cancellation);
            return;
        }

        await ReadRemoteAsync(key, consume, cancellation);
    }

    /// <summary>Передаёт объект из MinIO потоком</summary>
    /// <param name="key">Ключ объекта</param>
    /// <param name="consume">Получатель потока</param>
    /// <param name="cancellation">Отмена передачи</param>
    private async Task ReadRemoteAsync(string key, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellation)
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

    /// <summary>Сверяет локальный файл по размеру и контрольной сумме</summary>
    /// <param name="key">Изолированный серверный ключ</param>
    /// <param name="length">Ожидаемый размер</param>
    /// <param name="checksum">Ожидаемый SHA-256</param>
    /// <param name="cancellation">Отмена чтения</param>
    /// <returns>Файл подтверждён либо отсутствует</returns>
    private async Task<bool> ConfirmLocalAsync(string key, long length, string checksum, CancellationToken cancellation)
    {
        var path = LocalPath(key);
        if (!File.Exists(path))
        {
            return false;
        }

        if (new FileInfo(path).Length != length)
        {
            throw new IdentityCommandException(503, DocumentRules.Unavailable);
        }

        await using var file = File.OpenRead(path);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellation));
        if (!string.Equals(actual, checksum, StringComparison.Ordinal))
        {
            throw new IdentityCommandException(503, DocumentRules.Unavailable);
        }

        return true;
    }

    /// <summary>Сохраняет объект в локальную папку</summary>
    /// <param name="key">Изолированный серверный ключ</param>
    /// <param name="source">Поток содержимого</param>
    /// <param name="cancellation">Отмена передачи</param>
    private async Task SaveLocalAsync(string key, Stream source, CancellationToken cancellation)
    {
        var path = LocalPath(key);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None,
            64 * 1024, FileOptions.Asynchronous);
        await source.CopyToAsync(file, cancellation);
    }

    /// <summary>Путь локального файла по изолированному ключу</summary>
    /// <param name="key">Изолированный серверный ключ</param>
    /// <returns>Абсолютный или относительный путь файла</returns>
    private string LocalPath(string key)
    {
        var directory = _localDirectory ?? throw new IdentityCommandException(503, DocumentRules.Unavailable);
        return Path.Combine(directory, key.Replace('/', Path.DirectorySeparatorChar));
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
