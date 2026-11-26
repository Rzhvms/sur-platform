const cash = new Intl.NumberFormat('ru-RU', { style: 'currency', currency: 'RUB' });
const utc = new Intl.DateTimeFormat('ru-RU', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC' });
const calendar = new Intl.DateTimeFormat('ru-RU', { dateStyle: 'medium', timeZone: 'UTC' });

export function money(amount: number): string {
  return cash.format(amount);
}

export function utcTime(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '-' : `${utc.format(date)} UTC`;
}

export function calendarDate(value: string): string {
  const date = new Date(`${value}T00:00:00Z`);
  return /^\d{4}-\d{2}-\d{2}$/.test(value) && !Number.isNaN(date.getTime())
    && date.toISOString().slice(0, 10) === value ? calendar.format(date) : '-';
}
