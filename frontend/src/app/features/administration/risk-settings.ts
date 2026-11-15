import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { catchError, EMPTY, expand, forkJoin, map, of, reduce, startWith, switchMap } from 'rxjs';
import { errorMessage } from '../../core/http/api';
import { text } from '../../shared/text';
import { DataState, DataStateView } from '../../shared/ui/data-state';
import { ImpactScale } from '../risks/risks-api';
import { AdminApi, AdminWrite, Row, adminPageSize, adminText, rowPath } from './admin-api';

type ReferenceKind = 'HazardGroups' | 'Hazards' | 'Categories';
type Reference = Row & { description: string | null; groupId: string | null };
type Acceptance = { version: string; ownerMaxScore: number; directorMaxScore: number; ceoMaxScore: number };
type Approval = { version: string; ownerThreshold: number; directorThreshold: number; ceoThreshold: number };
type AssessmentConfig = { scale: ImpactScale | null; acceptance: Acceptance; approval: Approval };
const referenceRoute = 'risk-references/';
const settingsRoute = 'risk-assessment-settings/';
const approvalRoute = 'treatment-approval-settings';
const references: Record<ReferenceKind, string> = { HazardGroups: 'Группы опасностей', Hazards: 'Опасности', Categories: 'Категории' };
const labels = {
  scale: 'Денежная шкала ущерба', weight: 'Уровень', minLoss: 'Нижняя граница ущерба, ₽',
  acceptance: 'Границы принятия', approval: 'Пороги согласования мер, ₽',
  owner: 'Владелец процесса', director: 'Директор', ceo: 'Генеральный директор',
  scaleHint: 'Публикация шкалы пересчитает действующие оценки и может изменить доступный уровень принятия риска.',
  acceptanceHint: 'Обновить действующие оценки и проверить допустимость ранее принятых рисков',
  publish: 'Опубликовать шкалу',
} as const;

@Component({
  imports: [ReactiveFormsModule, DataStateView],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './risk-settings.html',
})
export class AdminRiskSettings {
  private readonly api = inject(AdminApi);
  private readonly refRevision = signal(0);
  private readonly configRevision = signal(0);
  protected readonly t = text;
  protected readonly w = adminText;
  protected readonly labels = labels;
  protected readonly names = references;
  protected readonly kinds = Object.keys(references) as ReferenceKind[];
  protected readonly kind = signal<ReferenceKind>('HazardGroups');
  protected readonly offset = signal(0);
  protected readonly size = adminPageSize;
  protected readonly selected = signal<Reference | 'new' | null>(null);
  protected readonly write = new AdminWrite(inject(DestroyRef));
  protected readonly reference = new FormGroup({
    name: new FormControl('', { nonNullable: true }), description: new FormControl('', { nonNullable: true }),
    groupId: new FormControl('', { nonNullable: true }), isActive: new FormControl(true, { nonNullable: true }),
  });
  protected readonly minimums = new FormArray(Array.from({ length: 5 }, () => new FormControl<number | null>(null)));
  protected readonly scaleConfirmed = new FormControl(false, { nonNullable: true });
  protected readonly acceptance = new FormGroup({
    owner: new FormControl<number | null>(null), director: new FormControl<number | null>(null), ceo: new FormControl<number | null>(null),
    applyToExisting: new FormControl(false, { nonNullable: true }), confirmed: new FormControl(false, { nonNullable: true }),
  });
  protected readonly approval = new FormGroup({
    owner: new FormControl<number | null>(null), director: new FormControl<number | null>(null), ceo: new FormControl<number | null>(null),
  });
  private readonly refQuery = computed(() => ({ kind: this.kind(), offset: this.offset(), revision: this.refRevision() }));
  protected readonly rows = toSignal(toObservable(this.refQuery).pipe(switchMap(({ kind, offset }) =>
    this.api.get<Reference[]>(referenceRoute + kind, { offset, limit: adminPageSize }).pipe(
      map((value): DataState<Reference[]> => ({ kind: 'ready', value })),
      catchError(error => of<DataState<Reference[]>>({ kind: 'error', message: errorMessage(error) })),
      startWith({ kind: 'loading' } as DataState<Reference[]>),
    ),
  )), { initialValue: { kind: 'loading' } as DataState<Reference[]> });
  protected readonly groups = toSignal(toObservable(this.kind).pipe(switchMap(kind => {
    if (kind !== 'Hazards') { return of([] as Reference[]); }
    const read = (offset: number) => this.api.get<Reference[]>(referenceRoute + 'HazardGroups', { offset, limit: adminPageSize });
    return read(0).pipe(
      expand((page, index) => page.length === adminPageSize ? read((index + 1) * adminPageSize) : EMPTY),
      reduce((all, page) => all.concat(page), [] as Reference[]),
      catchError(error => { this.write.notice.set(errorMessage(error)); return of([]); }),
    );
  })), { initialValue: [] });
  protected readonly config = toSignal(toObservable(this.configRevision).pipe(switchMap(() => forkJoin({
    scale: this.api.get<ImpactScale | null>(settingsRoute + 'impact-scale'),
    acceptance: this.api.get<Acceptance>(settingsRoute + 'acceptance'),
    approval: this.api.get<Approval>(approvalRoute),
  }).pipe(
    map((value): DataState<AssessmentConfig> => {
      this.fill(value);
      return { kind: 'ready', value };
    }),
    catchError(error => of<DataState<AssessmentConfig>>({ kind: 'error', message: errorMessage(error) })),
    startWith({ kind: 'loading' } as DataState<AssessmentConfig>),
  ))), { initialValue: { kind: 'loading' } as DataState<AssessmentConfig> });

  protected select(kind: ReferenceKind): void {
    this.kind.set(kind);
    this.offset.set(0);
    this.selected.set(null);
  }

  protected page(step: number): void { this.offset.update(value => Math.max(0, value + step * adminPageSize)); }
  protected reloadReferences(): void { this.refRevision.update(value => value + 1); }
  protected reloadConfig(): void { this.configRevision.update(value => value + 1); }

  protected open(row?: Reference): void {
    this.selected.set(row ?? 'new');
    this.reference.reset({
      name: row?.name ?? '', description: row?.description ?? '', groupId: row?.groupId ?? '',
      isActive: row?.isActive ?? true,
    });
  }

  protected saveReference(): void {
    const row = this.selected();
    if (!row) { return; }
    const kind = this.kind();
    const { name, description, groupId, isActive } = this.reference.getRawValue();
    if (!name.trim() || (kind === 'Hazards' && !groupId)) { this.write.notice.set(text.invalid); return; }
    const path = referenceRoute + kind;
    const body = { name: name.trim(), description: description.trim() || null, groupId: kind === 'Hazards' ? groupId : null, isActive };
    const request = row === 'new' ? this.api.post(path, body) : this.api.put(rowPath(path, row.id), { ...body, version: row.version });
    const reload = () => { this.selected.set(null); this.reloadReferences(); };
    this.write.run(request, reload, reload);
  }

  protected publish(scale: ImpactScale | null): void {
    const minimums = this.minimums.getRawValue();
    if (!this.scaleConfirmed.value || !strictlyIncreasing(minimums)) {
      this.write.notice.set(text.invalid); return;
    }
    this.write.run(this.api.put(settingsRoute + 'impact-scale', { version: scale?.version.id ?? null, minimums }),
      () => this.reloadConfig(), () => this.reloadConfig());
  }

  protected saveAcceptance(data: Acceptance): void {
    const { owner, director, ceo, applyToExisting, confirmed } = this.acceptance.getRawValue();
    if (!strictlyIncreasing([owner, director, ceo]) || ![owner, director, ceo].every(Number.isInteger)
      || (ceo ?? 25) >= 25 || (applyToExisting && !confirmed)) {
      this.write.notice.set(text.invalid); return;
    }
    const body = { version: data.version, ownerMaxScore: owner, directorMaxScore: director, ceoMaxScore: ceo, applyToExisting };
    this.write.run(this.api.put(settingsRoute + 'acceptance', body), () => this.reloadConfig(), () => this.reloadConfig());
  }

  protected saveApproval(data: Approval): void {
    const { owner, director, ceo } = this.approval.getRawValue();
    if (!strictlyIncreasing([owner, director, ceo])) { this.write.notice.set(text.invalid); return; }
    const body = { version: data.version, ownerThreshold: owner, directorThreshold: director, ceoThreshold: ceo };
    this.write.run(this.api.put(approvalRoute, body), () => this.reloadConfig(), () => this.reloadConfig());
  }

  private fill(value: AssessmentConfig): void {
    const positive = value.scale?.thresholds.filter(item => item.weight > 0).sort((a, b) => a.weight - b.weight);
    this.minimums.reset(positive?.map(item => item.minLossAmount) ?? [0, null, null, null, null]);
    this.scaleConfirmed.reset();
    this.acceptance.reset({
      owner: value.acceptance.ownerMaxScore, director: value.acceptance.directorMaxScore, ceo: value.acceptance.ceoMaxScore,
    });
    this.approval.reset({ owner: value.approval.ownerThreshold, director: value.approval.directorThreshold, ceo: value.approval.ceoThreshold });
  }
}

function strictlyIncreasing(values: (number | null)[]): boolean {
  let previous = -1;
  for (const value of values) {
    if (value === null || !Number.isFinite(value) || value < 0 || value <= previous) { return false; }
    previous = value;
  }
  return true;
}
