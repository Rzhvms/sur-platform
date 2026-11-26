import { DOCUMENT } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EMPTY, Observable, catchError, distinctUntilChanged, filter, finalize, map, of, startWith, switchMap } from 'rxjs';
import { Session } from '../../core/auth/session';
import { errorMessage, problemCode } from '../../core/http/api';
import { calendarDate, money, utcTime } from '../../shared/format';
import { statuses, text } from '../../shared/text';
import { DataState, DataStateView } from '../../shared/ui/data-state';
import { RiskComments } from '../risks/comments';
import { RiskDetails, readQuery, urlQuery } from '../risks/model';
import { RisksApi } from '../risks/risks-api';
import { ApprovalActions } from './approval-actions';
import { TreatmentDraftForm } from './draft';
import { JiraDeliveryView } from './jira-delivery';
import { Approval, Executor, Transition, Treatment, TreatmentCard, TreatmentDraft, approvalRights, approvalLevel, treatmentPageSize, treatmentStatuses, treatmentText, treatmentRegistryOffset } from './treatment-contract';
import { TreatmentsApi, treatmentWriteError } from './treatments-api';
import { pollWhileVisible } from './visible-poll';

type Card = { card: TreatmentCard; risk: RiskDetails };
type Command = { action: Transition | 'dispatch' | 'assign'; measure: Treatment };
const loading: DataState<Card> = { kind: 'loading' };

@Component({
  imports: [RouterLink, ReactiveFormsModule, DataStateView, RiskComments, ApprovalActions, TreatmentDraftForm, JiraDeliveryView],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './treatment-detail.html',
})
export class TreatmentDetail {
  private readonly api = inject(TreatmentsApi);
  private readonly risks = inject(RisksApi);
  private readonly session = inject(Session);
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly id = toSignal(this.route.paramMap.pipe(map(params => params.get('id') ?? ''), distinctUntilChanged()), { requireSync: true });
  private readonly params = toSignal(this.route.queryParamMap, { requireSync: true });
  private readonly revision = signal(0);
  private readonly denied = signal('');
  private readonly latestApprovalVersion = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly notice = signal('');
  protected readonly pollError = signal('');
  protected readonly edit = signal<Treatment | null>(null);
  protected readonly editConflict = signal(false);
  protected readonly command = signal<Command | null>(null);
  protected readonly confirmation = signal(false);
  protected readonly assignees = signal<DataState<Executor[]>>({ kind: 'loading' });
  protected readonly person = new FormControl('', { nonNullable: true });
  protected readonly actual = new FormControl<number | null>(null, Validators.min(0));
  protected readonly confirmed = new FormControl(false, { nonNullable: true });
  /** Подключает выбор исполнителя и параметры перехода к отправке Angular-формы */
  protected readonly commandForm = new FormGroup({ person: this.person, actual: this.actual, confirmed: this.confirmed });
  protected readonly t = text;
  protected readonly w = treatmentText;
  protected readonly statuses = treatmentStatuses;
  protected readonly riskStatuses = statuses;
  protected readonly money = money;
  protected readonly date = calendarDate;
  protected readonly time = utcTime;
  protected readonly level = approvalLevel;
  /** Доступ к очереди совпадает с правилами навигации; конкретный запрос проверяет сервер */
  protected readonly canOpenApprovals = computed(() => this.session.hasRight('admin') || approvalRights.some(right => this.session.hasRight(right)));
  protected readonly fromRegistry = computed(() => this.params().get('source') === 'registry');
  protected readonly registryQuery = computed(() => ({ offset: treatmentRegistryOffset(this.params().get('offset')) }));
  protected readonly fromApprovals = computed(() => this.params().get('source') === 'approvals');
  protected readonly riskQuery = computed(() => ({ ...urlQuery(readQuery(this.params())), from: this.params().get('from') === 'board' ? 'board' : null }));
  protected readonly pageSize = treatmentPageSize;
  protected readonly historyOffset = signal(0);
  private readonly historyQuery = computed(() => ({ id: this.id(), offset: this.historyOffset(), revision: this.revision(), denied: this.denied(), approvalVersion: this.latestApprovalVersion() }));
  protected readonly history = toSignal(toObservable(this.historyQuery).pipe(switchMap(({ id, offset, denied }) => denied ? EMPTY :
    this.api.history(id, offset).pipe(
      map((value): DataState<Approval[]> => ({ kind: 'ready', value })),
      catchError((error: unknown) => of<DataState<Approval[]>>({ kind: 'error', message: errorMessage(error) })),
    ),
  )), { initialValue: { kind: 'loading' } as DataState<Approval[]> });

  private readonly watcher = computed(() => ({ id: this.id(), revision: this.revision(), denied: this.denied() }));
  private readonly watcherChanges = toObservable(this.watcher);
  protected readonly result = toSignal(toObservable(this.id).pipe(switchMap(id => {
    this.reset();
    // Каждая запись перезапускает чтение. Просроченный ответ до записи больше не попадёт в карточку.
    let loaded = false;
    return this.watcherChanges.pipe(filter(query => query.id === id), switchMap(({ denied }) => {
      if (denied) {
        return of<DataState<Card>>({ kind: 'error', message: denied });
      }
      return pollWhileVisible(this.document, () => this.api.card(id).pipe(
        switchMap(card => this.risks.details(card.treatment.riskId).pipe(map(risk => ({ card, risk })))),
        map((value): DataState<Card> => {
          loaded = true;
          this.latestApprovalVersion.set(value.card.latestApproval?.version ?? null);
          this.pollError.set('');
          return { kind: 'ready', value };
        }),
        catchError((error: unknown) => {
          const message = errorMessage(error);
          if (error instanceof HttpErrorResponse && [401, 403, 404].includes(error.status)) {
            this.denied.set(message);
            return of<DataState<Card>>({ kind: 'error', message });
          }
          this.pollError.set(treatmentText.refreshError);
          return !loaded ? of<DataState<Card>>({ kind: 'error', message }) : EMPTY;
        }),
      ));
    }), startWith(loading));
  })), { initialValue: loading });

  protected get isAdmin(): boolean {
    return this.session.hasRight('admin');
  }

  protected owns(value: Card): boolean {
    const process = value.risk.processes.find(process => process.processId === value.card.treatment.processId);
    return this.isAdmin || (!!process?.ownerActive && process.ownerUserId === this.session.userId());
  }

  protected processes(risk: RiskDetails): { id: string; name: string }[] {
    return risk.processes.flatMap(process => process.riskProcessId ? [{ id: process.riskProcessId, name: process.processName }] : []);
  }

  protected processName(value: Card): string {
    const processId = value.card.treatment.processId;
    return value.risk.processes.find(process => process.processId === processId)?.processName ?? processId;
  }

  protected mayEdit(value: Card): boolean {
    return value.card.treatment.status === 'DRAFT' && this.owns(value) && this.session.hasRight('edit_treatment');
  }

  protected mayExecute(value: Card): boolean {
    return this.owns(value) && this.session.hasRight('execute_treatment');
  }

  protected mayDispatch(value: Card): boolean {
    const { treatment, requiredApprovalLevel } = value.card;
    return this.mayExecute(value) && this.session.hasRight('assign_treatment_executor') && value.risk.status === 'ACTIVE'
      && ((treatment.status === 'DRAFT' && !requiredApprovalLevel) || treatment.status === 'APPROVED');
  }

  protected mayAssign(value: Card): boolean {
    return value.card.jira.manualControl && this.owns(value) && this.session.hasRight('assign_treatment_executor')
      && ['WAITING', 'IN_PROGRESS', 'HOLD'].includes(value.card.treatment.status);
  }

  protected transitions(value: Card): Transition[] {
    if (!value.card.jira.manualControl || !this.mayExecute(value)) {
      return [];
    }
    switch (value.card.treatment.status) {
      case 'DRAFT':
      case 'WAITING': return ['cancel'];
      case 'IN_PROGRESS': return ['hold', 'complete', 'cancel'];
      case 'HOLD': return value.card.assignment && value.risk.status === 'IN_PROGRESS' ? ['resume', 'complete', 'cancel'] : ['complete', 'cancel'];
      default: return [];
    }
  }

  protected retry(message?: string): void {
    if (message !== undefined) {
      this.notice.set(message);
    }
    this.denied.set('');
    this.revision.update(value => value + 1);
  }

  protected pageHistory(step: number): void {
    this.historyOffset.update(value => Math.max(0, value + step * treatmentPageSize));
  }

  protected beginEdit(measure: Treatment): void {
    this.command.set(null);
    this.notice.set('');
    this.edit.set(measure);
    this.editConflict.set(false);
  }

  protected acceptVersion(measure: Treatment): void {
    this.edit.set(measure);
    this.editConflict.set(false);
    this.notice.set('');
  }

  protected saveDraft(draft: TreatmentDraft, process: string): void {
    const edit = this.edit();
    if (edit && !this.editConflict()) {
      this.run(this.api.edit(edit.id, edit.version, draft, process || edit.riskProcessId), () => this.edit.set(null), error => {
        if (error instanceof HttpErrorResponse && error.status === 409) {
          this.editConflict.set(true);
        }
      });
    }
  }

  protected submit(measure: Treatment): void {
    this.run(this.api.submit(measure.id, measure.version), () => {});
  }

  protected choose(action: Command['action'], measure: Treatment): void {
    if (this.busy()) {
      return;
    }
    this.edit.set(null);
    this.notice.set('');
    this.command.set({ action, measure });
    this.confirmation.set(false);
    this.confirmed.reset();
    this.person.reset();
    this.actual.setValue(measure.costActual);
    if (action === 'assign' || action === 'dispatch') {
      this.loadExecutors(measure.id);
    }
  }

  protected loadExecutors(id: string): void {
    const selection = this.command();
    this.assignees.set({ kind: 'loading' });
    this.api.executors(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: value => {
        if (this.command() === selection && this.id() === id) {
          this.assignees.set({ kind: 'ready', value });
        }
      },
      error: (error: unknown) => {
        if (this.command() === selection && this.id() === id) {
          this.assignees.set({ kind: 'error', message: errorMessage(error) });
        }
      },
    });
  }

  protected runCommand(unassign = false): void {
    const choice = this.command();
    if (!choice || (this.confirmation() && !this.confirmed.value)) {
      return;
    }
    const { measure: { id, version, status }, action } = choice;
    let request: Observable<unknown>;
    if (action === 'dispatch' || action === 'assign') {
      const assignee = this.person.value;
      const options = this.assignees();
      if ((!unassign && (options.kind !== 'ready' || !options.value.some(option => option.personId === assignee)))
        || (unassign && (action !== 'assign' || status !== 'HOLD'))) {
        return;
      }
      request = action === 'dispatch' ? this.api.dispatch(id, version, assignee)
        : this.api.assign(id, version, unassign ? null : assignee);
    } else {
      const cost = action === 'complete' ? this.actual.value : null;
      if (action === 'complete' && (this.actual.invalid || (cost !== null && !Number.isFinite(cost)))) {
        return;
      }
      request = this.api.transition(id, version, action, cost, this.confirmed.value);
    }
    this.run(request, () => this.command.set(null), () => this.command.set(null));
  }

  /** После ответа или сбоя перечитываем меру и риск, включая открывшийся пересмотр. */
  private run(request: Observable<unknown>, success: () => void, failed?: (error: unknown) => void): void {
    if (this.busy() || this.denied()) {
      return;
    }
    this.busy.set(true);
    this.notice.set('');
    request.pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false))).subscribe({
      next: () => {
        success();
        this.retry(treatmentText.saved);
      },
      error: (error: unknown) => {
        if (error instanceof HttpErrorResponse && problemCode(error) === 'TREATMENT_CONFIRMATION_REQUIRED') {
          this.confirmation.set(true);
        } else {
          failed?.(error);
          this.retry(treatmentWriteError(error));
        }
      },
    });
  }

  private reset(): void {
    this.edit.set(null);
    this.command.set(null);
    this.denied.set('');
    this.notice.set('');
    this.pollError.set('');
    this.latestApprovalVersion.set(null);
    this.historyOffset.set(0);
  }

}
