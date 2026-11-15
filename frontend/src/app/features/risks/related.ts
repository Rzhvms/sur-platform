import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { errorMessage } from '../../core/http/api';
import { calendarDate, utcTime } from '../../shared/format';
import { text } from '../../shared/text';
import { DataState, DataStateView } from '../../shared/ui/data-state';
import { reviewReasons, reviewSources, workflow } from './workflow-text';
import { RisksApi, historyPageSize } from './risks-api';

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
  imports: [DataStateView],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="panel" id="reviews">
      <h2>{{ t.reviews }}</h2>
      @let state = result();
      @if (!opened()) {
        <button type="button" (click)="opened.set(true)">{{ t.openSection }}</button>
      } @else if (state.kind === 'loading') {
        <sur-data-state kind="loading" />
      } @else if (state.kind === 'error') {
        <sur-data-state kind="error" [message]="state.message" (retry)="retry()" />
      } @else {
        <ul class="related-list">
          @for (item of state.value; track item.id) {
            <li><strong>{{ item.title }}</strong><span class="muted">{{ item.date }}</span>
              @if (item.source) {
                <dl class="fields">
                  <div><dt>{{ w.source }}</dt><dd>{{ item.source }}</dd></div>
                  <div><dt>{{ w.reasonLabel }}</dt><dd>{{ item.reason }}</dd></div>
                  <div><dt>{{ w.runStatus }}</dt><dd>{{ item.runStatus }}</dd></div>
                  <div><dt>{{ w.run }}</dt><dd><code>{{ item.runId }}</code></dd></div>
                  @if (item.scheduled) { <div><dt>{{ w.scheduled }}</dt><dd>{{ item.scheduled }}</dd></div> }
                </dl>
              }
              @if (item.detail) { <p class="user-content">{{ item.detail }}</p> }
            </li>
          } @empty { <li class="muted">{{ t.noReviews }}</li> }
        </ul>
        @if (offset() || state.value.length === pageSize) {
          <div class="pagination">
            <button type="button" [disabled]="offset() === 0" (click)="page(-1)">{{ t.previous }}</button>
            <button type="button" [disabled]="state.value.length < pageSize" (click)="page(1)">{{ t.next }}</button>
          </div>
        }
      }
    </section>
  `,
})
export class RiskRelated {
  readonly riskId = input.required<string>();
  private readonly api = inject(RisksApi);
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
