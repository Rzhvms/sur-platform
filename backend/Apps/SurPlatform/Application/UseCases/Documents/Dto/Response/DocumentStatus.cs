namespace Application.UseCases.Documents.Dto.Response;

/// <summary>Видимое состояние задания</summary>
/// <param name="Id">Идентификатор задания</param>
/// <param name="Kind">Назначение задания</param>
/// <param name="State">Подтверждённый этап обработки</param>
/// <param name="FileId">Готовый файл, когда результат подтверждён</param>
/// <param name="CreatedAt">Время регистрации</param>
/// <param name="AuthorId">Автор отчёта или импорта</param>
/// <param name="AsOf">Запрошенный либо фактический момент среза отчёта</param>
/// <param name="GeneratedAt">Время формирования подтверждённого результата</param>
/// <param name="CapturedAt">Время фиксации снимка в базе</param>
/// <param name="CompletedAt">Время завершения обработки</param>
/// <param name="Error">Код нарушения, если задание остановлено</param>
/// <param name="CanRetry">Допустимость повторной обработки</param>
/// <param name="Processed">Количество подтверждённых строк импорта</param>
/// <param name="Errors">Количество отклонённых строк импорта</param>
public sealed record DocumentStatus(Guid Id, string Kind, string State, Guid? FileId, DateTime CreatedAt,
    Guid AuthorId, DateTime? AsOf, DateTime? CapturedAt, DateTime? GeneratedAt, DateTime? CompletedAt, string? Error, bool CanRetry, long Processed, long Errors);
