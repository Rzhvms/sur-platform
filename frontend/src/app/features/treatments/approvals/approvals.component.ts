import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, of, switchMap } from 'rxjs';
import { errorMessage } from '../../../core/http/api';
import { money, utcTime } from '../../../shared/format';
import { text } from '../../../shared/text';
import { DataState, DataStateComponent } from '../../../shared/ui/data-state/data-state.component';
import { ApprovalActionsComponent } from '../approval-actions/approval-actions.component';
import { TreatmentQueue, approvalLevel, treatmentPageSize, treatmentText } from '../treatment-contract';
import { TreatmentsApiService } from '../treatments-api.service';

@Component({
  imports: [ApprovalActionsComponent, DataStateComponent, RouterLink],
  templateUrl: './approvals.component.html',
  styleUrl: './approvals.component.scss',
})
export class ApprovalsComponent {
  private readonly api = inject(TreatmentsApiService);
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
