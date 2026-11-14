namespace Infrastructure.Repositories.Risks.Interfaces;

/// <summary>Читает и исполняет сохранённые задания одного клиента</summary>
internal interface IDocumentWorkerRepository
{
    /// <summary>Обрабатывает ограниченное количество готовых заданий</summary>
    /// <param name="cancellation">Остановка службы</param>
    Task ProcessDocumentsAsync(CancellationToken cancellation);
}
