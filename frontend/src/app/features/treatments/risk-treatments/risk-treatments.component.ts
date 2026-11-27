import { DOCUMENT } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  DestroyRef,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import {
  takeUntilDestroyed,
  toObservable,
  toSignal,
} from '@angular/core/rxjs-interop';
import { RouterLink, ActivatedRoute } from '@angular/router';
import {
  EMPTY,
  catchError,
  finalize,
  map,
  of,
  startWith,
  switchMap,
} from 'rxjs';
import { SessionService } from '../../../core/auth/session.service';
import { errorMessage } from '../../../core/http/api';
import { calendarDate, money } from '../../../shared/format';
import { text } from '../../../shared/text';
import { ConfirmComponent } from '../../../shared/ui/confirm/confirm.component';
import {
  DataState,
  DataStateComponent,
} from '../../../shared/ui/data-state/data-state.component';
import { RiskDetails } from '../../risks/model';
import { RisksApiService } from '../../risks/risks-api.service';
import { TreatmentDraftFormComponent } from '../treatment-draft-form/treatment-draft-form.component';
import {
  Treatment,
  TreatmentDraft,
  treatmentPageSize,
  treatmentStatuses,
  treatmentText,
} from '../treatment-contract';
import {
  TreatmentsApiService,
  treatmentWriteError,
} from '../treatments-api.service';
import { pollWhileVisible } from '../visible-poll';

const loading: DataState<Treatment[]> = { kind: 'loading' };

@Component({
  selector: 'sur-risk-treatments',
  imports: [
    RouterLink,
    ConfirmComponent,
    DataStateComponent,
    TreatmentDraftFormComponent,
  ],
  templateUrl: './risk-treatments.component.html',
})
export class RiskTreatmentsComponent {
  readonly risk = input.required<RiskDetails>();
  readonly changed = output<void>();
  private readonly api = inject(TreatmentsApiService);
  private readonly risks = inject(RisksApiService);
  private readonly session = inject(SessionService);
  private readonly document = inject(DOCUMENT);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly confirm = viewChild.required(ConfirmComponent);
  private readonly revision = signal(0);
  private readonly denied = signal(false);
  private startVersion: string | null = null;
  protected readonly origin = toSignal(this.route.queryParams, {
    requireSync: true,
  });
  protected readonly t = text;
  protected readonly w = treatmentText;
  protected readonly statuses = treatmentStatuses;
  protected readonly pageSize = treatmentPageSize;
  protected readonly date = calendarDate;
  protected readonly money = money;
  protected readonly offset = signal(0);
  protected readonly editing = signal(false);
  protected readonly busy = signal(false);
  protected readonly notice = signal('');
  private readonly activeCycle = computed(() => {
    const risk = this.risk();
    return (
      risk.status === 'ACTIVE' &&
      ['treat', 'split'].includes(risk.currentAssessment?.responseMode ?? '')
    );
  });
  protected readonly processes = computed(() =>
    this.activeCycle() && this.session.hasRight('create_treatment')
      ? this.risk().processes.flatMap((process) =>
          process.riskProcessId
            ? [{ id: process.riskProcessId, name: process.processName }]
            : [],
        )
      : [],
  );
  protected readonly canStart = computed(() => {
    const primary = this.risk().processes.find((process) => process.isPrimary);
    return (
      this.activeCycle() &&
      this.session.hasRight('edit_risk') &&
      !!primary &&
      this.owns(primary)
    );
  });
  private readonly query = computed(() => ({
    id: this.risk().id,
    offset: this.offset(),
    revision: this.revision(),
  }));
  protected readonly result = toSignal(
    toObservable(this.query).pipe(
      switchMap(({ id, offset }) =>
        this.api.list(id, offset).pipe(
          map((value): DataState<Treatment[]> => ({ kind: 'ready', value })),
          startWith(loading),
          catchError((error: unknown) =>
            of<DataState<Treatment[]>>({
              kind: 'error',
              message: errorMessage(error),
            }),
          ),
        ),
      ),
    ),
    { initialValue: loading },
  );

  protected readonly startReady = computed(() => {
    const state = this.result();
    if (state.kind !== 'ready') {
      return false;
    }
    if (this.offset() > 0 || state.value.length === treatmentPageSize) {
      // Полное состояние набора проверяется командой в транзакции.
      return true;
    }
    const active = state.value.filter((item) =>
      ['PENDING', 'APPROVED', 'WAITING', 'IN_PROGRESS', 'HOLD'].includes(
        item.status,
      ),
    );
    return (
      active.length > 0 && active.every((item) => item.status === 'WAITING')
    );
  });

  // Получаем только адресную проекцию карточки, не перезагружая соседние формы риска.
  private readonly watch = computed(() =>
    !this.denied() && this.risk().status === 'IN_PROGRESS'
      ? this.risk().id
      : null,
  );
  protected readonly jiraPending = toSignal(
    toObservable(this.watch).pipe(
      switchMap((id) =>
        id === null
          ? of(false)
          : pollWhileVisible(this.document, () =>
              this.risks.details(id).pipe(
                map((card) => !!card.jiraRetryPending),
                catchError((error: unknown) => {
                  if (
                    error instanceof HttpErrorResponse &&
                    [401, 403, 404].includes(error.status)
                  ) {
                    this.denied.set(true);
                    return of(false);
                  }
                  return EMPTY;
                }),
              ),
            ).pipe(startWith(!!this.risk().jiraRetryPending)),
      ),
    ),
    { initialValue: false },
  );

  protected processName(processId: string): string {
    return (
      this.risk().processes.find((process) => process.processId === processId)
        ?.processName ?? processId
    );
  }

  protected retry(): void {
    this.revision.update((value) => value + 1);
  }

  protected page(step: number): void {
    this.offset.update((value) =>
      Math.max(0, value + step * treatmentPageSize),
    );
  }

  protected askStart(opener: HTMLButtonElement): void {
    if (this.canStart() && this.startReady() && !this.busy()) {
      this.startVersion = this.risk().version;
      this.confirm().show(opener);
    }
  }

  protected start(): void {
    if (!this.startVersion || this.busy() || !this.canStart()) {
      return;
    }
    this.busy.set(true);
    this.notice.set('');
    this.api
      .start(this.risk().id, this.startVersion)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.busy.set(false)),
      )
      .subscribe({
        next: () => {
          this.notice.set(treatmentText.saved);
          this.retry();
          this.changed.emit();
        },
        error: (error: unknown) => {
          this.notice.set(treatmentWriteError(error));
          this.retry();
          this.changed.emit();
        },
      });
  }

  protected create(riskProcessId: string, draft: TreatmentDraft): void {
    if (
      this.busy() ||
      !this.processes().some((process) => process.id === riskProcessId)
    ) {
      return;
    }
    this.notice.set('');
    this.busy.set(true);
    this.api
      .create(this.risk().id, riskProcessId, draft)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.busy.set(false)),
      )
      .subscribe({
        next: () => {
          this.editing.set(false);
          this.notice.set(treatmentText.saved);
          this.offset.set(0);
          this.retry();
        },
        error: (error: unknown) => {
          this.notice.set(treatmentWriteError(error));
          this.retry();
        },
      });
  }

  private owns(process: {
    ownerActive?: boolean;
    ownerUserId?: string;
  }): boolean {
    return (
      this.session.hasRight('admin') ||
      (!!process.ownerActive && this.session.userId() === process.ownerUserId)
    );
  }
}
