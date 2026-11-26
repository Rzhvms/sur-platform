import { DestroyRef, Injectable, Injector, afterNextRender, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationCancel, NavigationError, NavigationStart, Router } from '@angular/router';

/** Измеряет навигацию до отображения готовых данных без записи маршрута, UUID и параметров поиска. */
@Injectable({ providedIn: 'root' })
export class UiTiming {
  private readonly injector = inject(Injector);
  private startedAt: number | null = null;
  private sequence = 0;

  constructor() {
    inject(Router).events.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe(event => {
      if (event instanceof NavigationStart) {
        this.startedAt = performance.now();
        this.sequence++;
      } else if (event instanceof NavigationCancel || event instanceof NavigationError) {
        this.startedAt = null;
      }
    });
  }

  ready(view: 'registry' | 'board' | 'risk-card'): void {
    const start = this.startedAt;
    const sequence = this.sequence;
    if (start === null) { return; }
    this.startedAt = null;
    afterNextRender(() => {
      if (sequence !== this.sequence) { return; }
      const name = 'sur:' + view;
      performance.clearMeasures(name);
      performance.measure(name, { start, end: performance.now() });
    }, { injector: this.injector });
  }
}
