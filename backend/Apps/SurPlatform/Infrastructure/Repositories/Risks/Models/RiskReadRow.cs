namespace Infrastructure.Repositories.Risks.Models;

/// <summary>Данные RiskReadRow в клиентской базе</summary>
internal sealed record RiskReadRow
{
    /// <summary>Колонка</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Паспорт</summary>
    public string? RiskJson { get; init; }

    /// <summary>Текущая оценка</summary>
    public string? AssessmentJson { get; init; }

    /// <summary>Действующие связи</summary>
    public string? ProcessesJson { get; init; }

    /// <summary>Название опасности</summary>
    public string HazardName { get; init; } = string.Empty;

    /// <summary>Название категории</summary>
    public string CategoryName { get; init; } = string.Empty;

    /// <summary>Объект</summary>
    public string? AssetName { get; init; }

    /// <summary>ФИО автора</summary>
    public string? AuthorName { get; init; }
}
