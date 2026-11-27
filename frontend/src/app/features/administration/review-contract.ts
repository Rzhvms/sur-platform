export interface ScheduleFields {
  startDate: string;
  recurrenceType: string;
  recurrenceInterval: number | null;
  weekdays: number[];
  endType: string;
  occurrenceCount: number | null;
  endDate: string;
}

/** Передаём календарные строки и только параметры выбранного правила повтора. */
export function schedulePayload(fields: ScheduleFields) {
  const weekly = fields.recurrenceType === 'WEEKLY';
  const recurring = fields.recurrenceType !== 'NONE';
  const endType = recurring ? fields.endType : 'NEVER';
  const validDates = validCalendarDate(fields.startDate) &&
    (endType !== 'ON_DATE' || (validCalendarDate(fields.endDate) && fields.endDate >= fields.startDate));
  const limited = endType === 'AFTER_OCCURRENCES';
  if (!validDates || (recurring && !positiveInteger(fields.recurrenceInterval))
    || (weekly && (!fields.weekdays.length || fields.weekdays.some(day => !Number.isInteger(day) || day < 1 || day > 7)
      || new Set(fields.weekdays).size !== fields.weekdays.length)) || (limited && !positiveInteger(fields.occurrenceCount))) {
    return null;
  }
  return {
    startDate: fields.startDate, recurrenceType: fields.recurrenceType,
    recurrenceInterval: recurring ? fields.recurrenceInterval : null,
    weekdays: weekly ? fields.weekdays : [],
    endType, occurrenceCount: limited ? fields.occurrenceCount : null,
    endDate: endType === 'ON_DATE' ? fields.endDate : null,
  };
}

function validCalendarDate(value: string): boolean {
  if (value.startsWith('0000-') || !/^\d{4}-\d{2}-\d{2}$/.test(value)) { return false; }
  const date = new Date(value + 'T00:00:00Z');
  return !Number.isNaN(date.getTime()) && date.toISOString().slice(0, 10) === value;
}

function positiveInteger(value: number | null): boolean {
  return value !== null && Number.isInteger(value) && value > 0;
}
