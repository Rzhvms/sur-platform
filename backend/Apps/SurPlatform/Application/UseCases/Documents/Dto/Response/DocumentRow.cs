using System.Text.Json;

namespace Application.UseCases.Documents.Dto.Response;

/// <summary>Строка сохранённого снимка или результат импорта</summary>
/// <param name="Number">Номер записи</param>
/// <param name="Data">Зафиксированные поля</param>
/// <param name="HasError">Признак отклонения строки импорта</param>
public sealed record DocumentRow(long Number, JsonElement Data, bool HasError);
