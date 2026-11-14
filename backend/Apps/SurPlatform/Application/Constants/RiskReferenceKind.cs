using System.Text.Json.Serialization;

namespace Application.Constants;

/// <summary>Допустимые справочники риска</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RiskReferenceKind>))]
public enum RiskReferenceKind
{
    /// <summary>Группы опасностей</summary>
    HazardGroups,

    /// <summary>Опасности</summary>
    Hazards,

    /// <summary>Категории</summary>
    Categories
}
