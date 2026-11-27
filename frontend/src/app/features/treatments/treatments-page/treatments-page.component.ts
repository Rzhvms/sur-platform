import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Subject, catchError, combineLatest, map, of, startWith, switchMap } from 'rxjs';
import { errorMessage } from '../../../core/http/api';
import { calendarDate, money } from '../../../shared/format';
import { text } from '../../../shared/text';
import { DataState, DataStateComponent } from '../../../shared/ui/data-state/data-state.component';
import { TreatmentRegistry, treatmentPageSize, treatmentRegistryOffset, treatmentStatuses, treatmentText } from '../treatment-contract';
import { TreatmentsApiService } from '../treatments-api.service';

const loading: DataState<TreatmentRegistry> = { kind: 'loading' };

/** Общий список мер клиента с серверной пагинацией и восстановлением страницы по URL */
@Component({
  imports: [RouterLink, DataStateComponent],
  templateUrl: './treatments-page.component.html',
  styleUrl: './treatments-page.component.scss',
})
export class TreatmentsPageComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(TreatmentsApiService);
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
