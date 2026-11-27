namespace Infrastructure.Repositories.Risks.Models;

/// <summary>Наблюдение, полученное через авторизованный GET Jira</summary>
/// <param name="Id">Устойчивый идентификатор Jira</param>
/// <param name="Key">Текущий ключ задачи</param>
/// <param name="Status">Идентификатор статуса</param>
/// <param name="Assignee">Логин исполнителя</param>
/// <param name="UpdatedAt">Версия наблюдения Jira</param>
/// <param name="Summary">Заголовок для сверки</param>
/// <param name="Description">Описание для сверки</param>
internal sealed record JiraIssueSnapshot(string Id, string Key, string Status, string? Assignee,
    DateTimeOffset UpdatedAt, string? Summary, string? Description);
