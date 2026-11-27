namespace Application.UseCases.Documents.Dto.Request;

/// <summary>Регистрация загрузки источника CSV</summary>
public sealed record ImportRequest
{
    /// <summary>Устойчивый ключ операции</summary>
    public Guid OperationId { get; init; }

    /// <summary>Ожидаемое количество байт</summary>
    public long Size { get; init; }

    /// <summary>SHA-256 источника в hex</summary>
    public string Checksum { get; init; } = string.Empty;
}
