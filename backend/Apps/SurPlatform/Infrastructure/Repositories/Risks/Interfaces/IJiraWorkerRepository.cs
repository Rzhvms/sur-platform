namespace Infrastructure.Repositories.Risks.Interfaces;

/// <summary>Фоновая обработка из выбранной клиентской базы</summary>
internal interface IJiraWorkerRepository
{
    /// <summary>Обрабатывает доступные поручения и сверяет подтверждённые связи</summary>
    /// <param name="cancellation">Остановка службы</param>
    Task ProcessJiraAsync(CancellationToken cancellation);
}
