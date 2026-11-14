namespace Application.UseCases.Jira.Dto.Request;

/// <summary>Параметры чтения каталогов Jira перед сохранением подключения</summary>
public sealed record JiraDiscoveryRequest
{
    /// <summary>Адрес Jira из списка разрешённых</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>REST-секрет, если ещё не сохранён</summary>
    public string? RestToken { get; init; }

    /// <summary>Проект для чтения типов и статусов</summary>
    public string? Project { get; init; }
}
