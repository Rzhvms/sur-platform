using System.Text.Json.Nodes;

namespace Infrastructure.Repositories.Risks.Models;

/// <summary>Значения изменённого поля бизнес-сущности</summary>
/// <param name="Path">Путь поля в сущности или её дочерней записи</param>
/// <param name="OldValue">Значение перед операцией</param>
/// <param name="NewValue">Значение после операции</param>
internal sealed record BusinessFieldChange(string Path, JsonNode? OldValue, JsonNode? NewValue);
