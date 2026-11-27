import { Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { errorMessage } from '../../../core/http/api';
import { calendarDate, utcTime } from '../../../shared/format';
import { text } from '../../../shared/text';
import { DataState, DataStateComponent } from '../../../shared/ui/data-state/data-state.component';
import { reviewReasons, reviewSources, workflow } from '../workflow-text';
import { RisksApiService, historyPageSize } from '../risks-api.service';

interface RelatedRow {
  id: string; title: string; detail: string | null; date: string;
  source?: string; reason?: string; runStatus?: string; runId?: string; scheduled?: string;
}

const loading: DataState<RelatedRow[]> = { kind: 'loading' };
const reviewStatus: Record<string, string> = {
  WAITING: 'Ожидает пересмотра', PROCESSING: 'В процессе', COMPLETED: 'Завершён',
  NO_ACTION: 'Без изменений', ERROR: 'Ошибка', COMPLETED_WITH_ERRORS: 'Завершён с ошибками',
};

@Component({
  selector: 'sur-risk-related',
  imports: [DataStateComponent],
  templateUrl: './risk-related.component.html',
})
export class RiskRelatedComponent {
  readonly riskId = input.required<string>();
  private readonly api = inject(RisksApiService);
  private readonly sequence = signal(0);
  protected readonly t = text;
  protected readonly w = workflow;
  protected readonly pageSize = historyPageSize;
  protected readonly offset = signal(0);
  protected readonly opened = signal(false);
  private readonly query = computed(() => ({
    opened: this.opened(), id: this.riskId(), offset: this.offset(), sequence: this.sequence(),
  }));

  protected readonly result = toSignal(toObservable(this.query).pipe(switchMap(({ opened, id, offset }) => {
    if (!opened) {
      return of(loading);
    }
    const request = this.api.reviews(id, offset).pipe(map(items => items.map(({ review, run }): RelatedRow => ({
      id: review.id, title: review.status === 'WAITING' ? workflow.reviewRequired : reviewStatus[review.status] ?? review.status,
      detail: review.closeReason ?? review.errorMessage, date: utcTime(run.occurredAt),
      source: [reviewSources[run.sourceType] ?? run.sourceType, run.sourceNameSnapshot, run.externalSystemNameSnapshot].filter(Boolean).join(' · '),
      reason: reviewReasons[run.reason] ?? run.reason,
      runStatus: run.status === 'COMPLETED' ? workflow.reviewComplete : reviewStatus[run.status] ?? run.status,
      runId: run.id, scheduled: run.scheduledDate ? calendarDate(run.scheduledDate.slice(0, 10)) : undefined,
    }))));
    return request.pipe(
      map((value): DataState<RelatedRow[]> => ({ kind: 'ready', value })),
      catchError((error: unknown) => of<DataState<RelatedRow[]>>({ kind: 'error', message: errorMessage(error) })),
      startWith(loading),
    );
  })), { initialValue: loading });

  protected page(direction: number): void {
    this.offset.update(offset => Math.max(0, offset + direction * historyPageSize));
  }

  protected retry(): void {
    this.sequence.update(value => value + 1);
  }
}
