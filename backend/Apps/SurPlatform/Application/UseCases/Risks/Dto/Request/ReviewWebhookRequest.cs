namespace Application.UseCases.Risks.Dto.Request;

/// <summary>Поля административной команды пересмотра</summary>
public sealed record ReviewWebhookRequest
{
    /// <summary>Ожидаемая версия</summary>
    public Guid? Version { get; init; }

    /// <summary>Административное название</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Название внешней системы</summary>
    public string ExternalSystemName { get; init; } = string.Empty;

    /// <summary>Активный ответственный</summary>
    public Guid ResponsibleUserId { get; init; }

    /// <summary>Разрешён ли приём</summary>
    public bool IsActive { get; init; } = true;

    /// <summary>Полный набор рисков</summary>
    public Guid[]? RiskIds { get; init; }
}
