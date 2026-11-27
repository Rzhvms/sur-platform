import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Observable, Subject, catchError, finalize, map, of, startWith, switchMap, takeUntil } from 'rxjs';
import { SessionService } from '../../../core/auth/session.service';
import { errorMessage, problemCode } from '../../../core/http/api';
import { money, utcTime } from '../../../shared/format';
import { text } from '../../../shared/text';
import { DataState, DataStateComponent } from '../../../shared/ui/data-state/data-state.component';
import { RatingComponent, scoreBand } from '../../../shared/ui/rating/rating.component';
import { Assessment, RiskDetails } from '../model';
import { AssessmentInputs, assessmentInputs, assessmentSubmission } from '../assessment-contract';
import { AssessmentMatrix, AssessmentPreview, ImpactScale, RisksApiService, historyPageSize } from '../risks-api.service';
import { acceptanceLabels, workflow } from '../workflow-text';

const loading = { kind: 'loading' } as const;

@Component({
  selector: 'sur-risk-assessment',
  imports: [ReactiveFormsModule, RatingComponent, DataStateComponent],
  templateUrl: './risk-assessment.component.html',
  styleUrl: './risk-assessment.component.scss',
})
export class RiskAssessmentComponent {
  readonly risk = input.required<RiskDetails>();
  readonly changed = output<void>();

  private readonly api = inject(RisksApiService);
  private readonly session = inject(SessionService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly cancelPreview = new Subject<void>();
  private readonly freshCard = signal<RiskDetails | null>(null);
  private readonly epoch = signal(0);
  private readonly matrixReload = signal(0);
  private readonly historyReload = signal(0);
  private readonly scaleReload = signal(0);
  private readonly assessment = signal<Assessment | null>(null);
  protected readonly page = signal(0);
  protected readonly pageSize = historyPageSize;
  protected readonly needsRefresh = signal(false);
  protected readonly pending = signal<'preview' | 'save' | 'refresh' | null>(null);
  protected readonly error = signal('');
  protected readonly preview = signal<{ response: AssessmentPreview; inputs: AssessmentInputs } | null>(null);
  protected readonly w = workflow;
  protected readonly t = text;
  protected readonly money = money;
  protected readonly utc = utcTime;
  protected readonly band = scoreBand;
  protected readonly acceptanceLabels = acceptanceLabels;
  protected readonly card = computed(() => this.freshCard() ?? this.risk());
  protected readonly hasAsset = computed(() => !!this.card().assetId);
  protected readonly selected = computed(() => this.assessment() ?? this.card().currentAssessment ?? this.card().previousAssessment);
  protected readonly allowed = computed(() => {
    const risk = this.card();
    const owner = risk.processes.find(process => process.isPrimary);
    const owns = this.session.rights().includes('admin') || owner?.ownerActive && owner.ownerUserId === this.session.userId();
    return owns && this.session.hasRight('assess_risk') && (
      risk.status === 'DRAFT' && this.session.hasRight('edit_risk') ||
      risk.status === 'AWAITING_REVIEW' && risk.needsHumanAssessment && risk.currentAssessment === null);
  });

  protected readonly form = new FormGroup({
    likelihoodLevelId: new FormControl('', { nonNullable: true }),
    lossAmount: new FormControl<number | null>(null),
    downtimeWeeks: new FormControl<number | null>(null),
    downtimeDays: new FormControl<number | null>(null),
    downtimeHours: new FormControl<number | null>(null),
  });

  protected readonly inputs = computed(() => {
    this.epoch();
    return assessmentInputs(this.form.getRawValue(), this.hasAsset());
  });

  protected readonly matrix = toSignal(toObservable(this.matrixReload).pipe(
    switchMap(() => this.read(this.api.matrix())),
  ), { initialValue: loading });

  private readonly historyQuery = computed(() => ({ id: this.risk().id, page: this.page(), reload: this.historyReload() }));
  protected readonly history = toSignal(toObservable(this.historyQuery).pipe(
    switchMap(({ id, page }) => this.read(this.api.assessments(id, page * historyPageSize))),
  ), { initialValue: loading });

  private readonly scaleQuery = computed(() => ({ id: this.selected()?.scaleVersionId, reload: this.scaleReload() }));
  protected readonly scale = toSignal(toObservable(this.scaleQuery).pipe(
    switchMap(({ id }) => this.read<ImpactScale | null>(id ? this.api.scale(id) : of(null))),
  ), { initialValue: loading });

  protected readonly positiveLikelihoods = computed(() => {
    const matrix = this.matrix();
    return matrix.kind === 'ready' ? matrix.value.likelihoodLevels.filter(level => level.weight > 0) : [];
  });
  protected readonly positiveImpacts = computed(() => {
    const matrix = this.matrix();
    return matrix.kind === 'ready' ? matrix.value.impactLevels.filter(level => level.weight > 0) : [];
  });

  constructor() {
    this.form.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      // Любое изменение, даже возврат прежнего значения, отменяет старый расчёт.
      this.epoch.update(value => value + 1);
      this.preview.set(null);
      this.cancelPreview.next();
    });
  }

  protected selectScale(assessment: Assessment): void {
    this.assessment.set(assessment);
  }

  protected reload(part: 'matrix' | 'history' | 'scale'): void {
    const signalToUpdate = part === 'matrix' ? this.matrixReload : part === 'history' ? this.historyReload : this.scaleReload;
    signalToUpdate.update(value => value + 1);
  }

  protected levelName(matrix: AssessmentMatrix, id: string | undefined, axis: 'likelihood' | 'impact'): string {
    const levels = axis === 'likelihood' ? matrix.likelihoodLevels : matrix.impactLevels;
    const level = levels.find(item => item.id === id);
    return level ? `${level.weight} · ${level.name}` : text.unknown;
  }

  protected calculate(): void {
    const inputs = this.inputs();
    if (!inputs || this.pending() || this.needsRefresh() || !this.allowed()) {
      return;
    }
    const epoch = this.epoch();
    this.preview.set(null);
    this.pending.set('preview');
    this.error.set('');
    this.api.preview(this.risk().id, inputs).pipe(
      takeUntil(this.cancelPreview), takeUntilDestroyed(this.destroyRef),
      finalize(() => this.pending.set(null)),
    ).subscribe({
      next: response => {
        if (this.epoch() === epoch) {
          if (response.riskVersion !== this.card().version) {
            this.needsRefresh.set(true);
            this.error.set(workflow.assessmentConflict);
          } else {
            this.preview.set({ response, inputs });
          }
        }
      },
      error: (error: unknown) => this.report(error),
    });
  }

  protected save(): void {
    const preview = this.preview();
    if (!preview || this.pending() || !this.allowed()) {
      return;
    }
    this.pending.set('save');
    this.preview.set(null);
    this.error.set('');
    this.api.saveAssessment(this.risk().id, assessmentSubmission(preview.inputs, preview.response)).pipe(
      takeUntilDestroyed(this.destroyRef), finalize(() => this.pending.set(null)),
    ).subscribe({
      next: () => this.changed.emit(),
      error: (error: unknown) => {
        this.report(error);
        if (error instanceof HttpErrorResponse && error.status === 409) {
          this.needsRefresh.set(true);
        }
      },
    });
  }

  protected refreshForPreview(): void {
    if (this.pending()) {
      return;
    }
    this.pending.set('refresh');
    this.api.details(this.risk().id).pipe(
      takeUntilDestroyed(this.destroyRef), finalize(() => this.pending.set(null)),
    ).subscribe({
      next: risk => {
        this.freshCard.set(risk);
        this.needsRefresh.set(false);
        this.error.set(this.allowed() ? workflow.assessmentConflict : workflow.invalidState);
        this.reload('history');
      },
      error: (error: unknown) => this.report(error),
    });
  }

  private report(error: unknown): void {
    if (error instanceof HttpErrorResponse) {
      const code = problemCode(error);
      if (code === 'RISK_SCALE_REQUIRED') {
        this.error.set(workflow.noPublishedScale);
        return;
      }
      if (code === 'STALE_VERSION') {
        this.error.set(workflow.assessmentConflict);
        this.needsRefresh.set(true);
        return;
      }
      if (code === 'RISK_INVALID_TRANSITION') {
        this.error.set(workflow.invalidState);
        this.needsRefresh.set(true);
        return;
      }
      if (this.pending() === 'save' && (error.status === 0 || error.status >= 500)) {
        this.error.set(text.unknownWrite);
        this.needsRefresh.set(true);
        return;
      }
    }
    this.error.set(errorMessage(error));
  }

  private read<T>(request: Observable<T>): Observable<DataState<T>> {
    return request.pipe(map((value): DataState<T> => ({ kind: 'ready', value })),
      catchError((error: unknown) => of<DataState<T>>({ kind: 'error', message: errorMessage(error) })),
      startWith(loading));
  }
}
