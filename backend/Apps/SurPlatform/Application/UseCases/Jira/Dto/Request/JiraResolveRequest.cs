namespace Application.UseCases.Jira.Dto.Request;

/// <summary>Результат административной сверки задачи</summary>
public sealed record JiraResolveRequest
{
    /// <summary>Время начала сверяемой попытки из карточки меры</summary>
    public DateTimeOffset? AttemptStartedAt { get; init; }

    /// <summary>Идентификатор или ключ найденной задачи</summary>
    public string? Issue { get; init; }

    /// <summary>Явное подтверждение отсутствия задачи</summary>
    public bool ConfirmAbsent { get; init; }
}
