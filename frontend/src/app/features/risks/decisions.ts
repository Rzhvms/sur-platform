import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, finalize, map, of, startWith, switchMap } from 'rxjs';
import { Session } from '../../core/auth/session';
import { errorMessage, problemCode } from '../../core/http/api';
import { text } from '../../shared/text';
import { DataState, DataStateView } from '../../shared/ui/data-state';
import { validSplit } from './assessment-contract';
import { guid, RiskDetails, RiskOption } from './model';
import { RiskAction, RiskActionBody, RisksApi } from './risks-api';
import { acceptanceLabels, actionLabels, responseModeLabels, workflow } from './workflow-text';

type Candidate = RiskOption & { ownerName: string | null };
const loading = { kind: 'loading' } as const;
const acceptanceRights: Record<string, string> = {
  OWNER: 'accept_owner_risk', DIRECTOR: 'accept_director_risk', CEO: 'accept_ceo_risk',
};

@Component({
  selector: 'sur-risk-decisions',
  imports: [RouterLink, DataStateView],
  templateUrl: './decisions.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RiskDecisions {
  readonly risk = input.required<RiskDetails>();
  readonly changed = output<void>();
  private readonly api = inject(RisksApi);
  private readonly session = inject(Session);
  private readonly destroyRef = inject(DestroyRef);
  private readonly optionReload = signal(0);
  protected readonly w = workflow;
  protected readonly t = text;
  protected readonly labels = actionLabels;
  protected readonly acceptanceLabels = acceptanceLabels;
  protected readonly responseLabels = responseModeLabels;
  protected readonly action = signal<RiskAction | null>(null);
  protected readonly pending = signal(false);
  protected readonly stale = signal(false);
  protected readonly error = signal('');
  protected readonly dependencies = signal<string[]>([]);
  protected readonly selected = signal<string[]>([]);
  protected readonly primary = signal('');
  protected readonly reason = signal('');
  protected readonly confirmed = signal(false);

  protected readonly actions = computed<RiskAction[]>(() => {
    const risk = this.risk();
    const admin = this.session.rights().includes('admin');
    const owner = risk.processes.find(process => process.isPrimary);
    const owns = admin || owner?.ownerActive && owner.ownerUserId === this.session.userId();
    const actions: RiskAction[] = [];
    if (risk.status === 'ACTIVE' && risk.currentAssessment && !risk.currentAssessment.responseMode) {
      const level = risk.acceptanceLevel;
      const right = level ? acceptanceRights[level] : undefined;
      // Назначения директора и CEO не входят в паспорт риска. Их проверяет команда API.
      if (level && (admin || (right && this.session.hasRight(right) && (level !== 'OWNER' || owns)))) {
        actions.push('accept');
      }
      if (owns && this.session.hasRight('edit_risk')) { actions.push('treat'); }
      if (owns && this.session.hasRight('split_risk')) { actions.push('split'); }
      if (owns && this.session.hasRight('transfer_risk')) { actions.push('transfer'); }
    }
    if (owns && this.session.hasRight('close_risk') && ['DRAFT', 'ACTIVE', 'AWAITING_REVIEW', 'ACCEPTED'].includes(risk.status)) {
      actions.push('close');
    }
    if (owns && this.session.hasRight('edit_risk') && ['ACCEPTED', 'CLOSED'].includes(risk.status)) {
      actions.push('reopen');
    }
    return actions;
  });

  private readonly optionQuery = computed(() => ({ id: this.risk().id, action: this.action(), reload: this.optionReload() }));
  protected readonly candidates = toSignal(toObservable(this.optionQuery).pipe(switchMap(({ id, action }) => {
    if (action !== 'split' && action !== 'transfer') {
      return of<DataState<Candidate[]>>({ kind: 'ready', value: [] });
    }
    return this.api.decisionProcesses(id, action).pipe(
      map((value): DataState<Candidate[]> => ({ kind: 'ready', value })),
      catchError((error: unknown) => {
        this.report(error, false);
        return of<DataState<Candidate[]>>({ kind: 'error', message: errorMessage(error) });
      }),
      startWith(loading),
    );
  })), { initialValue: loading });

  protected readonly eligible = computed(() => {
    const action = this.action();
    if (!action || !this.actions().includes(action)) {
      return false;
    }
    switch (action) {
      case 'split':
      case 'transfer': {
        const candidates = this.candidates();
        if (candidates.kind !== 'ready') { return false; }
        const options = candidates.value;
        return action === 'transfer'
          ? options.some(item => item.id === this.primary())
          : validSplit(this.selected(), this.primary()) && this.selected().every(id => options.some(item => item.id === id));
      }
      case 'close': return this.reason().trim().length > 0 && this.reason().trim().length <= 4000;
      case 'reopen': return this.risk().status !== 'CLOSED' || this.confirmed();
      default: return true;
    }
  });

  protected choose(action: RiskAction): void {
    this.action.set(action);
    this.primary.set('');
    this.selected.set([]);
    this.reason.set('');
    this.confirmed.set(false);
    this.resetError();
  }

  protected retryOptions(): void {
    this.resetError();
    this.optionReload.update(value => value + 1);
  }

  protected toggleProcess(id: string, selected: boolean): void {
    this.selected.update(ids => selected ? [...ids, id] : ids.filter(item => item !== id));
    if (!selected && this.primary() === id) {
      this.primary.set('');
    }
  }

  protected submit(): void {
    const action = this.action();
    if (!action || !this.eligible() || this.pending() || this.stale()) {
      return;
    }
    const risk = this.risk();
    const body: RiskActionBody = { version: risk.version };
    switch (action) {
      case 'split':
        body.processIds = this.selected();
        body.primaryProcessId = this.primary();
        break;
      case 'transfer':
        body.processId = this.primary();
        break;
      case 'close':
        body.reason = this.reason().trim();
        break;
      case 'reopen':
        if (risk.status === 'CLOSED') { body.confirmed = this.confirmed(); }
        break;
    }

    this.resetError();
    this.pending.set(true);
    this.api.decision(risk.id, action, body).pipe(
      takeUntilDestroyed(this.destroyRef), finalize(() => this.pending.set(false)),
    ).subscribe({
      next: () => this.changed.emit(),
      error: (error: unknown) => this.report(error, true),
    });
  }

  private report(error: unknown, write: boolean): void {
    if (error instanceof HttpErrorResponse) {
      const messages: Record<string, string> = {
        TREATMENT_STATE_CONFLICT: workflow.blockers, RISK_INVALID_TRANSITION: workflow.invalidState,
        RISK_MODE_SELECTED: workflow.alreadyDecided, RISK_REFERENCE_INACTIVE: workflow.inactiveProcess,
      };
      const code = problemCode(error);
      const body: unknown = error.error;
      if (body && typeof body === 'object' && 'dependencyIds' in body && Array.isArray(body.dependencyIds)) {
        this.dependencies.set(body.dependencyIds.filter((id): id is string => typeof id === 'string' && guid.test(id)));
      }
      if (write && (error.status === 0 || error.status >= 500)) {
        this.stale.set(true);
        this.error.set(text.unknownWrite);
        return;
      }
      this.stale.set(code === 'STALE_VERSION' || code === 'RISK_MODE_SELECTED' || code === 'RISK_INVALID_TRANSITION');
      this.error.set((code && messages[code]) || errorMessage(error));
      return;
    }
    this.error.set(errorMessage(error));
  }

  private resetError(): void {
    this.error.set('');
    this.dependencies.set([]);
  }
}
