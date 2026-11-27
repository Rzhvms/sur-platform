import { EMPTY, Observable, distinctUntilChanged, exhaustMap, fromEvent, map, startWith, switchMap, timer } from 'rxjs';

const updateInterval = 25_000;

/** При скрытии отменяем запрос. На видимой вкладке начинаем с чтения и пропускаем перекрывающиеся тики. */
export function pollWhileVisible<T>(document: Document, read: () => Observable<T>): Observable<T> {
  return fromEvent(document, 'visibilitychange').pipe(
    startWith(undefined),
    map(() => document.visibilityState === 'visible'),
    distinctUntilChanged(),
    switchMap(visible => visible ? timer(0, updateInterval).pipe(exhaustMap(() => read())) : EMPTY),
  );
}
