import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subject, catchError, combineLatest, map, of, startWith, switchMap } from 'rxjs';
import { errorMessage } from '../../core/http/api';
import { calendarDate, money } from '../../shared/format';
import { text } from '../../shared/text';
import { DataState, DataStateView } from '../../shared/ui/data-state';
import { TreatmentRegistry, treatmentPageSize, treatmentRegistryOffset, treatmentStatuses, treatmentText } from './treatment-contract';
import { TreatmentsApi } from './treatments-api';

const loading: DataState<TreatmentRegistry> = { kind: 'loading' };

/** Общий список мер клиента с серверной пагинацией и восстановлением страницы по URL */
@Component({
  imports: [RouterLink, DataStateView],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="page-heading treatment-registry-heading">
      <h1>{{ t.treatments }}</h1>
      <div class="treatment-actions"><button type="button" (click)="refresh()">{{ t.refresh }}</button><a class="action-link" routerLink="/risks">{{ w.toRisks }}</a></div>
    </header>
    <p class="treatment-registry-hint">{{ w.registryHint }}</p>
    @let state = result();
    @if (state.kind === 'loading') { <sur-data-state kind="loading" /> }
    @else if (state.kind === 'error') { <sur-data-state kind="error" [message]="state.message" (retry)="refresh()" /> }
    @else {
      <ul class="risk-treatment-list treatment-registry-list">
        @for (item of state.value.items; track item.id) {
          <li class="risk-treatment-card">
            <div class="risk-treatment-heading">
              <a class="treatment-title-link" [routerLink]="['/treatments', item.id]" [queryParams]="{ source: 'registry', offset: offset() }"><span>{{ item.title }}</span><span aria-hidden="true">↗</span></a>
              <span class="treatment-state" [attr.data-state]="item.status">{{ statuses[item.status] ?? t.unknown }}</span>
            </div>
            <div class="treatment-registry-risk"><span>{{ w.registryRisk }}</span><a [routerLink]="['/risks', item.riskId]" fragment="treatments">{{ state.value.riskNames[item.riskId] ?? t.unknown }}</a></div>
            <p class="treatment-process">{{ state.value.processNames[item.processId] ?? t.unknown }}</p>
            <dl class="treatment-facts">
              <div><dt>{{ t.due }}</dt><dd>{{ date(item.dueDate.slice(0, 10)) }}</dd></div>
              <div><dt>{{ w.plan }}</dt><dd>{{ money(item.costPlanned) }}</dd></div>
              <div><dt>{{ w.actual }}</dt><dd>{{ item.costActual === null ? t.unknown : money(item.costActual) }}</dd></div>
            </dl>
          </li>
        } @empty {
          <li class="treatments-entry"><h2>{{ t.noTreatments }}</h2><p>{{ w.registryEmpty }}</p><a class="action-link primary" routerLink="/risks">{{ w.toRisks }}</a></li>
        }
      </ul>
      @if (offset() || state.value.hasMore) {
        <nav class="pagination" [attr.aria-label]="t.treatments">
          @if (offset()) { <a class="action-link" routerLink="/treatments" [queryParams]="{ offset: offset() - pageSize }">{{ t.previous }}</a> }
          @if (state.value.hasMore && canAdvance()) { <a class="action-link" routerLink="/treatments" [queryParams]="{ offset: offset() + pageSize }">{{ t.next }}</a> }
        </nav>
      }
    }
  `,
})
export class Treatments {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(TreatmentsApi);
  private readonly reload = new Subject<void>();
  private readonly query = toSignal(this.route.queryParamMap, { requireSync: true });
  protected readonly t = text;
  protected readonly w = treatmentText;
  protected readonly statuses = treatmentStatuses;
  protected readonly date = calendarDate;
  protected readonly money = money;
  protected readonly pageSize = treatmentPageSize;
  protected readonly offset = computed(() => treatmentRegistryOffset(this.query().get('offset')));
  protected readonly canAdvance = computed(() => treatmentRegistryOffset(String(this.offset() + treatmentPageSize)) > this.offset());
  protected readonly result = toSignal(combineLatest([toObservable(this.offset), this.reload.pipe(startWith(undefined))]).pipe(
    switchMap(([offset]) => this.api.registry(offset).pipe(
      map((value): DataState<TreatmentRegistry> => ({ kind: 'ready', value })),
      startWith(loading),
      catchError((error: unknown) => of<DataState<TreatmentRegistry>>({ kind: 'error', message:
        error instanceof HttpErrorResponse && error.status === 404 ? treatmentText.registryUnavailable : errorMessage(error) })),
    )),
  ), { initialValue: loading });

  /** Повторяет чтение текущей страницы после ошибки или изменения данных */
  protected refresh(): void {
    this.reload.next();
  }
}
