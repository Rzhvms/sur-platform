import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, of, switchMap } from 'rxjs';
import { errorMessage } from '../../core/http/api';
import { money, utcTime } from '../../shared/format';
import { text } from '../../shared/text';
import { DataState, DataStateView } from '../../shared/ui/data-state';
import { ApprovalActions } from './approval-actions';
import { TreatmentQueue, approvalLevel, treatmentPageSize, treatmentText } from './treatment-contract';
import { TreatmentsApi } from './treatments-api';

@Component({
  imports: [ApprovalActions, DataStateView, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-heading approval-page-heading"><h1>{{ w.approvals }}</h1><button type="button" (click)="refresh()">{{ t.refresh }}</button></div>
    @if (notice()) { <p class="notice approval-page-notice" role="status">{{ notice() }}</p> }
    @let state = result();
    @if (state.kind === 'loading') { <sur-data-state kind="loading" /> }
    @else if (state.kind === 'error') { <sur-data-state kind="error" [message]="state.message" (retry)="refresh()" /> }
    @else {
      <p class="approval-queue-count" role="status">{{ w.queue }} <strong>{{ state.value.count }}</strong></p>
      <div class="approval-queue-list">
      @for (item of state.value.items; track item.id) {
        <section class="panel approval-queue-card">
          <header class="approval-queue-card-heading"><h2><a [routerLink]="['/treatments', item.treatmentId]" [queryParams]="{ source: 'approvals' }">{{ state.value.treatmentTitles?.[item.treatmentId] ?? w.openTreatment }}</a></h2><span class="approval-level-badge">{{ level(item.requiredLevel) }}</span></header>
          <dl class="approval-queue-facts">
            <div><dt>{{ w.level }}</dt><dd>{{ level(item.requiredLevel) }}</dd></div>
            <div><dt>{{ w.plan }}</dt><dd>{{ money(item.costPlanned) }}</dd></div>
            <div><dt>{{ w.trigger }}</dt><dd>{{ money(item.triggerThreshold) }}</dd></div>

            <div><dt>{{ t.created }}</dt><dd>{{ time(item.requestedAt) }}</dd></div>
          </dl>
          <details class="approval-thresholds"><summary>{{ w.savedThresholds }}</summary>
            <dl class="approval-queue-facts">
              <div><dt>{{ w.ownerThreshold }}</dt><dd>{{ money(item.ownerThreshold) }}</dd></div>
              <div><dt>{{ w.directorThreshold }}</dt><dd>{{ money(item.directorThreshold) }}</dd></div>
              <div><dt>{{ w.ceoThreshold }}</dt><dd>{{ money(item.ceoThreshold) }}</dd></div>
            </dl>
          </details>
          <sur-approval-actions [approval]="item" [inQueue]="true" (changed)="refresh($event)" />
        </section>
      } @empty { <p class="panel muted">{{ w.noApprovals }}</p> }
      </div>
      @if (offset() || offset() + pageSize < state.value.count) {
        <div class="pagination">
          <button type="button" [disabled]="offset() === 0" (click)="page(-1)">{{ t.previous }}</button>
          <button type="button" [disabled]="offset() + pageSize >= state.value.count" (click)="page(1)">{{ t.next }}</button>
        </div>
      }
    }
  `,
})
export class Approvals {
  private readonly api = inject(TreatmentsApi);
  private readonly revision = signal(0);
  protected readonly offset = signal(0);
  protected readonly notice = signal('');
  protected readonly t = text;
  protected readonly w = treatmentText;
  protected readonly money = money;
  protected readonly time = utcTime;
  protected readonly level = approvalLevel;
  protected readonly pageSize = treatmentPageSize;
  private readonly query = computed(() => ({ offset: this.offset(), revision: this.revision() }));
  protected readonly result = toSignal(toObservable(this.query).pipe(switchMap(({ offset }) => this.api.queue(offset).pipe(
    map((value): DataState<TreatmentQueue> => {
      // После решения последней записи на странице возвращаемся к предыдущей.
      if (!value.items.length && offset > 0) {
        this.page(-1);
      }
      return { kind: 'ready', value };
    }),
    catchError((error: unknown) => of<DataState<TreatmentQueue>>({ kind: 'error', message: errorMessage(error) })),
  ))), { initialValue: { kind: 'loading' } as DataState<TreatmentQueue> });

  protected page(step: number): void {
    this.offset.update(value => Math.max(0, value + step * treatmentPageSize));
  }

  protected refresh(message = ''): void {
    this.notice.set(message);
    this.revision.update(value => value + 1);
  }
}
