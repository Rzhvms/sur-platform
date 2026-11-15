import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EMPTY, Observable, Subject, catchError, combineLatest, finalize, forkJoin, map, of, startWith, switchMap, tap } from 'rxjs';
import { Session } from '../../core/auth/session';
import { errorMessage } from '../../core/http/api';
import { text } from '../../shared/text';
import { DataState, DataStateView } from '../../shared/ui/data-state';
import { PassportFields, RiskDetails, RiskOption, RiskReference, readQuery, urlQuery } from './model';
import { RisksApi } from './risks-api';

interface EditorData {
  risk: RiskDetails | null;
  groups: RiskReference[];
  hazards: RiskReference[];
  categories: RiskOption[];
  assets: RiskOption[];
  processes: RiskOption[];
}

const emptyData: EditorData = { risk: null, groups: [], hazards: [], categories: [], assets: [], processes: [] };
const load: DataState<EditorData> = { kind: 'loading' };

@Component({
  selector: 'sur-risk-editor',
  imports: [RouterLink, ReactiveFormsModule, DataStateView],
  templateUrl: './editor.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RiskEditor {
  private readonly api = inject(RisksApi);
  private readonly session = inject(Session);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly reload = new Subject<void>();
  private readonly currentRisk = signal<RiskDetails | null>(null);
  private readonly params = toSignal(this.route.queryParamMap, { requireSync: true });
  private readonly id = toSignal(this.route.paramMap.pipe(map(params => params.get('id'))), { requireSync: true });

  protected readonly t = text;
  protected readonly needsComparison = signal(false);
  protected readonly conflict = signal<RiskDetails | null>(null);
  protected readonly pending = signal(false);
  protected readonly error = signal('');
  protected readonly editing = computed(() => this.id() !== null);
  protected readonly canWrite = computed(() => this.editing()
    ? this.currentRisk()?.passportEditable === true && this.session.hasRight('edit_risk')
    : this.session.hasRight('create_risk'));
  protected readonly query = computed(() => urlQuery(readQuery(this.params())));
  protected readonly back = computed(() => this.id() ? ['/risks', this.id()] :
    this.params().get('from') === 'board' ? ['/risks', 'board'] : ['/risks']);
  protected readonly returnQuery = computed(() => ({ ...this.query(), from: this.id() ? this.params().get('from') : null }));

  protected readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(250)]],
    description: ['', [Validators.required, Validators.maxLength(16000)]],
    groupId: '',
    hazardId: ['', Validators.required],
    categoryId: ['', Validators.required],
    assetId: '',
    processId: '',
  });
  protected readonly formValue = toSignal(this.form.valueChanges.pipe(map(() => this.form.getRawValue())),
    { initialValue: this.form.getRawValue() });

  protected readonly data = toSignal(combineLatest([this.route.paramMap, this.reload.pipe(startWith(undefined))]).pipe(
    switchMap(([params]) => {
      const id = params.get('id');
      const risk: Observable<RiskDetails | null> = id ? this.api.details(id) : of(null);
      return risk.pipe(switchMap(value => {
        const editable = value ? value.passportEditable && this.session.hasRight('edit_risk') : this.session.hasRight('create_risk');
        if (!editable) {
          return of({ ...emptyData, risk: value });
        }
        return forkJoin({
          risk: of(value),
          groups: this.api.references('HazardGroups'),
          hazards: this.api.references('Hazards'),
          categories: this.api.references('Categories'),
          assets: this.api.assets(),
          processes: id ? of([] as RiskOption[]) : this.api.processes(),
        });
      }), tap(value => this.loadForm(value)),
      map((value): DataState<EditorData> => ({ kind: 'ready', value })),
      catchError((error: unknown) => of<DataState<EditorData>>({ kind: 'error', message: errorMessage(error) })),
      startWith(load));
    }),
  ), { initialValue: load });

  protected groupChanged(): void {
    this.form.controls.hazardId.setValue('');
  }

  protected availableHazards(data: EditorData): RiskReference[] {
    return data.hazards.filter(hazard => hazard.groupId === this.formValue().groupId);
  }

  protected hasOption(options: RiskOption[], value: string): boolean {
    return options.some(option => option.id === value);
  }

  protected previewName(options: RiskOption[], id: string): string {
    return options.find(option => option.id === id)?.name ?? (id || text.unknown);
  }

  protected acceptVersion(): void {
    const updated = this.conflict();
    if (updated?.passportEditable) {
      this.currentRisk.set(updated);
      this.conflict.set(null);
      this.needsComparison.set(false);
      this.error.set('');
    }
  }

  protected retry(): void {
    this.reload.next();
  }

  protected retryComparison(): void {
    const id = this.id();
    if (!id || this.pending()) {
      return;
    }
    this.pending.set(true);
    this.readConflict(id).pipe(
      takeUntilDestroyed(this.destroyRef), finalize(() => this.pending.set(false)),
    ).subscribe();
  }

  /** Сохраняем версию, которую видел пользователь, и не повторяем запись автоматически. */
  protected save(): void {
    const value = this.form.getRawValue();
    const id = this.id();
    const current = this.currentRisk();
    if (this.pending() || this.needsComparison() || !this.canWrite() || this.form.invalid
      || !value.name.trim() || !value.description.trim() || (!id && !value.processId)) {
      return;
    }

    const fields: PassportFields = {
      name: value.name.trim(), description: value.description.trim(), hazardId: value.hazardId,
      categoryId: value.categoryId, assetId: value.assetId || null,
    };
    const write = id && current ? this.api.update(id, { ...fields, version: current.version })
      : this.api.create(fields, value.processId);

    this.pending.set(true);
    this.error.set('');
    write.pipe(
      catchError((error: unknown) => {
        if (this.id() !== id) {
          return EMPTY;
        }
        this.error.set(this.writeError(error));
        if (!(error instanceof HttpErrorResponse) || error.status !== 409 || !id) {
          return EMPTY;
        }
        this.needsComparison.set(true);
        return this.readConflict(id);
      }),
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.pending.set(false)),
    ).subscribe(result => {
      if (this.id() !== id) {
        return;
      }
      void this.router.navigate(['/risks', result.id], {
        queryParams: { ...this.query(), from: this.params().get('from') }, replaceUrl: true,
      });
    });
  }

  private readConflict(id: string): Observable<never> {
    return this.api.details(id).pipe(
      tap(fresh => {
        if (this.id() === id) {
          this.conflict.set(fresh);
        }
      }),
      switchMap(() => EMPTY),
      catchError((error: unknown) => {
        if (this.id() === id) {
          this.error.set(`${errorMessage(error)} ${text.keepDraft}`);
        }
        return EMPTY;
      }),
    );
  }

  private writeError(error: unknown): string {
    if (error instanceof HttpErrorResponse && (error.status === 0 || error.status >= 500)) {
      return text.unknownWrite;
    }
    const body: unknown = error instanceof HttpErrorResponse ? error.error : null;
    return body && typeof body === 'object' && 'code' in body && body.code === 'RISK_REFERENCE_INACTIVE'
      ? text.unavailableOption : errorMessage(error);
  }

  private loadForm(data: EditorData): void {
    const value = data.risk;
    this.currentRisk.set(value);
    this.needsComparison.set(false);
    this.conflict.set(null);
    this.error.set('');
    this.form.reset({
      name: value?.name ?? '', description: value?.description ?? '',
      groupId: data.hazards.find(hazard => hazard.id === value?.hazardId)?.groupId ?? '',
      hazardId: value?.hazardId ?? '', categoryId: value?.categoryId ?? '', assetId: value?.assetId ?? '',
      processId: '',
    });
  }
}
