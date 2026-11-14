using Domain.DalModels;

namespace Api.Controllers.Provider.Risks.Dto.Response;

/// <summary>Настройка пересмотра с календарными датами в HTTP-ответе</summary>
/// <param name="Id">Идентификатор настройки</param>
/// <param name="Version">Версия административной формы</param>
/// <param name="Name">Название источника</param>
/// <param name="IsActive">Разрешены ли новые запуски</param>
/// <param name="ResponsibleUserId">Ответственный за источник</param>
/// <param name="CreatedById">Кто создал настройку</param>
/// <param name="UpdatedById">Кто последним изменил настройку</param>
/// <param name="CreatedAt">Время создания UTC</param>
/// <param name="UpdatedAt">Время последнего изменения UTC</param>
/// <param name="TimeZone">Сохранённая календарная зона</param>
/// <param name="StartDate">Календарная дата начала</param>
/// <param name="RecurrenceType">Периодичность</param>
/// <param name="RecurrenceInterval">Интервал периодичности</param>
/// <param name="Weekdays">Дни недели в формате ISO</param>
/// <param name="EndType">Условие окончания</param>
/// <param name="OccurrenceCount">Предельное число срабатываний</param>
/// <param name="EndDate">Последняя допустимая дата</param>
public sealed record ReviewScheduleResponse(
    Guid Id,
    Guid Version,
    string Name,
    bool IsActive,
    Guid ResponsibleUserId,
    Guid CreatedById,
    Guid UpdatedById,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string TimeZone,
    DateOnly StartDate,
    string RecurrenceType,
    int? RecurrenceInterval,
    int[] Weekdays,
    string EndType,
    int? OccurrenceCount,
    DateOnly? EndDate)
{
    /// <summary>Преобразует сохранённое расписание в контракт HTTP</summary>
    /// <param name="schedule">Сохранённое расписание</param>
    /// <returns>Настройка с датами без времени суток</returns>
    internal static ReviewScheduleResponse FromDal(ReviewScheduleDal schedule)
    {
        return new ReviewScheduleResponse(
            schedule.Id,
            schedule.Version,
            schedule.Name,
            schedule.IsActive,
            schedule.ResponsibleUserId,
            schedule.CreatedById,
            schedule.UpdatedById,
            schedule.CreatedAt,
            schedule.UpdatedAt,
            schedule.TimeZone,
            DateOnly.FromDateTime(schedule.StartDate),
            schedule.RecurrenceType,
            schedule.RecurrenceInterval,
            schedule.Weekdays,
            schedule.EndType,
            schedule.OccurrenceCount,
            schedule.EndDate is DateTime endDate ? DateOnly.FromDateTime(endDate) : null);
    }
}
