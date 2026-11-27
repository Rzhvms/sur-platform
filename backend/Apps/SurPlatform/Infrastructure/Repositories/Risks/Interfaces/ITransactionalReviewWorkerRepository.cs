namespace Infrastructure.Repositories.Risks.Interfaces;

/// <summary>Фоновый пересмотр в уже выбранной клиентской базе</summary>
internal interface ITransactionalReviewWorkerRepository
{
    /// <summary>Регистрирует наступившие даты и продолжает сохранённые запуски</summary>
    /// <param name="cancellationToken">Остановка фоновой службы</param>
    Task ProcessReviewsAsync(CancellationToken cancellationToken);
}
