using Application.Constants;

namespace Application.UseCases.Documents.Dto.Request;

/// <summary>Сопоставление столбцов CSV с командой регистрации риска</summary>
public sealed record ImportMapping
{
    /// <summary>Разделитель CSV по умолчанию запятая</summary>
    public string Delimiter { get; init; } = DocumentRules.DefaultDelimiter;

    /// <summary>Пропуск первой записи CSV</summary>
    public bool HasHeader { get; init; } = true;

    /// <summary>Индексы столбцов с нуля по именам полей запроса</summary>
    public Dictionary<string, int> Columns { get; init; } = [];
}
