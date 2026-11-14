using Application.Constants;
using Application.Exceptions;
using Application.UseCases.Risks.Dto.Request;
using Domain.DalModels;

namespace Infrastructure.Repositories.Risks.Helpers;

/// <summary>Календарные правила в сохранённой зоне настройки</summary>
internal static class ReviewCalendar
{
    /// <summary>Проверяет согласованность полей календарного правила</summary>
    /// <param name="request">Поля административной формы</param>
    internal static void Validate(ReviewScheduleRequest request)
    {
        var weekly = request.RecurrenceType == ReviewRules.Weekly;
        var once = request.RecurrenceType == ReviewRules.Once;
        var weekdays = request.Weekdays ?? [];
        var validRepeat = request.RecurrenceType is ReviewRules.Weekly or ReviewRules.Monthly or ReviewRules.Yearly;
        var validEnd = request.EndType switch
        {
            ReviewRules.Never => request.OccurrenceCount is null && request.EndDate is null,
            ReviewRules.AfterOccurrences => request.OccurrenceCount is > 0 && request.EndDate is null,
            ReviewRules.OnDate => request.OccurrenceCount is null && request.EndDate is DateOnly end && end >= request.StartDate,
            _ => false
        };
        if (request.StartDate == default || !(once || validRepeat) || !validEnd
            || (once ? request.RecurrenceInterval is not null || request.EndType != ReviewRules.Never : request.RecurrenceInterval is not > 0)
            || (weekly ? weekdays.Length == 0 || weekdays.Any(day => day is < 1 or > 7) || weekdays.Distinct().Count() != weekdays.Length : weekdays.Length != 0))
        {
            throw new IdentityCommandException(400, ReviewRules.Invalid);
        }
    }

    /// <summary>Определяет, совпадает ли локальная дата с правилом расписания</summary>
    /// <param name="schedule">Сохранённое расписание</param>
    /// <param name="day">Локальная календарная дата</param>
    /// <returns>Совпадение даты с повторением и датой окончания</returns>
    internal static bool IsDue(ReviewScheduleDal schedule, DateOnly day)
    {
        var start = DateOnly.FromDateTime(schedule.StartDate);
        if (day < start || schedule.EndDate is DateTime end && day > DateOnly.FromDateTime(end))
        {
            return false;
        }

        var interval = schedule.RecurrenceInterval.GetValueOrDefault(1);
        var months = (day.Year - start.Year) * 12 + day.Month - start.Month;
        var clampedDay = Math.Min(start.Day, DateTime.DaysInMonth(day.Year, day.Month));
        return schedule.RecurrenceType switch
        {
            ReviewRules.Once => day == start,
            ReviewRules.Weekly => ((day.DayNumber - start.DayNumber + IsoDay(start) - 1) / 7) % interval == 0
                && schedule.Weekdays.Contains(IsoDay(day)),
            ReviewRules.Monthly => months % interval == 0 && day.Day == clampedDay,
            ReviewRules.Yearly => day.Month == start.Month && (day.Year - start.Year) % interval == 0 && day.Day == clampedDay,
            _ => false
        };
    }

    /// <summary>Переводит день недели из .NET в ISO</summary>
    /// <param name="day">Календарная дата</param>
    /// <returns>Число от понедельника 1 до воскресенья 7</returns>
    private static int IsoDay(DateOnly day)
    {
        return day.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)day.DayOfWeek;
    }
}
