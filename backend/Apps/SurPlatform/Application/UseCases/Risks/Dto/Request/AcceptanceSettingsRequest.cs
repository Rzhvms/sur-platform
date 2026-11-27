namespace Application.UseCases.Risks.Dto.Request;

/// <summary>Настройка маршрутов встроенного метода</summary>
public sealed record AcceptanceSettingsRequest
{
    /// <summary>Версия настройки</summary>
    public Guid Version { get; init; }

    /// <summary>Верхняя граница владельца</summary>
    public int OwnerMaxScore { get; init; }

    /// <summary>Верхняя граница директора</summary>
    public int DirectorMaxScore { get; init; }

    /// <summary>Верхняя граница CEO</summary>
    public int CeoMaxScore { get; init; }

    /// <summary>Пересчитать маршруты активных рисков и рисков в работе</summary>
    public bool ApplyToExisting { get; init; }
}
