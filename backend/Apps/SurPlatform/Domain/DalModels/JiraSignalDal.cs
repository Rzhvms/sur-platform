namespace Domain.DalModels;

/// <summary>Данные обмена с Jira в клиентской базе</summary>
public sealed record JiraSignalDal
{
    /// <summary>Ключ локальной записи</summary>
    public Guid Id { get; init; }

    /// <summary>Устойчивый идентификатор внешней задачи</summary>
    public string IssueId { get; init; } = string.Empty;

    /// <summary>Момент регистрации сигнала</summary>
    public DateTime CreatedAt { get; init; }
}
