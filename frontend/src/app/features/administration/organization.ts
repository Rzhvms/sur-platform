import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { catchError, finalize, forkJoin, map, of, startWith, switchMap } from 'rxjs';
import { errorMessage } from '../../core/http/api';
import { text } from '../../shared/text';
import { DataState, DataStateView } from '../../shared/ui/data-state';
import { AdminApi, AdminWrite, Person, ResponsibilityType, Row, Slot, User, adminPageSize, adminText, firstCursor, responsibilityLabels, rowPath } from './admin-api';

type Directory = 'directorates' | 'persons' | 'processes' | 'assets' | 'responsibilities';
type OrgRow = Omit<Row, 'name'> & {
  name?: string; fullName?: string; email?: string; directorateId?: string | null; ownerId?: string;
  positionName?: string; userId?: string; personId?: string; description?: string; responsibilityType?: ResponsibilityType;
  inventoryNumber?: string; hourlyDowntimeCost?: number | null;
};
type Mode = 'edit' | 'create' | 'owner' | 'replace' | 'transfer' | 'state';
type Editor = { mode: Mode; row?: OrgRow };
const directoryPath = 'organization/';
const loading: DataState<OrgRow[]> = { kind: 'loading' };
const directories: Directory[] = ['directorates', 'persons', 'processes', 'assets', 'responsibilities'];
const orgLabels = { inventory: 'Инвентарный номер', downtime: 'Стоимость часа простоя, ₽', available: 'Все состояния' };
const field = () => new FormControl('', { nonNullable: true });

@Component({
  imports: [ReactiveFormsModule, RouterLink, DataStateView],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './organization.html',
})
export class AdminOrganization {
  private readonly api = inject(AdminApi);
  private readonly destroy = inject(DestroyRef);
  private readonly revision = signal(0);
  protected readonly t = text;
  protected readonly w = adminText;
  protected readonly labels = orgLabels;
  protected readonly types = Object.keys(responsibilityLabels) as ResponsibilityType[];
  protected readonly roleLabels = responsibilityLabels;
  protected readonly directories = directories;
  protected readonly kind = signal<Directory>('directorates');
  protected readonly cursors = signal([firstCursor]);
  protected readonly search = field();
  protected readonly filter = signal('');
  protected readonly stateFilter = signal('');
  protected readonly size = adminPageSize;
  protected readonly write = new AdminWrite(this.destroy);
  protected readonly editor = signal<Editor | null>(null);
  protected readonly observation = signal<{ key: string; slot: Slot } | null>(null);
  protected readonly checking = signal(false);
  protected readonly form = new FormGroup({
    name: field(), description: field(), userId: field(), directorateId: field(), positionName: field(),
    ownerId: field(), personId: field(), inventoryNumber: field(), responsibilityType: field(),
    hourlyDowntimeCost: new FormControl<number | null>(null), confirmed: new FormControl(false, { nonNullable: true }),
    assignCeo: new FormControl(false, { nonNullable: true }),
  });
  private readonly query = computed(() => ({
    kind: this.kind(), after: this.cursors().at(-1) ?? firstCursor, search: this.filter(),
    active: this.stateFilter(), revision: this.revision(),
  }));
  protected readonly data = toSignal(toObservable(this.query).pipe(switchMap(({ kind, after, search, active }) =>
    this.api.get<OrgRow[]>(directoryPath + kind, {
      after, limit: adminPageSize, ...(search ? { search } : {}), ...(active ? { active } : {}),
    }).pipe(
      map((value): DataState<OrgRow[]> => ({ kind: 'ready', value })),
      catchError(error => of<DataState<OrgRow[]>>({ kind: 'error', message: errorMessage(error) })),
      startWith(loading),
    ),
  )), { initialValue: loading });

  private readonly optionQuery = computed(() => ({ kind: this.kind(), revision: this.revision() }));
  protected readonly options = toSignal(toObservable(this.optionQuery).pipe(switchMap(({ kind }) => forkJoin({
    directorates: kind === 'directorates' || kind === 'assets' ? of([] as Row[]) :
      this.api.options<Row>(directoryPath + 'directorates', row => row.id, { active: true }),
    people: kind === 'processes' || kind === 'responsibilities' ?
      this.api.options<Person>(directoryPath + 'persons', row => row.id, { active: true }) : of([] as Person[]),
    users: kind === 'persons' || kind === 'responsibilities' ?
      this.api.options<User>('users', row => row.userId).pipe(map(users => users.filter(user => user.isActive))) : of([] as User[]),
  }).pipe(catchError(error => {
    this.write.notice.set(errorMessage(error));
    return of({ directorates: [], people: [], users: [] });
  })))), { initialValue: { directorates: [], people: [], users: [] } });

  protected select(kind: Directory): void {
    this.editor.set(null);
    this.write.notice.set('');
    this.kind.set(kind);
    this.stateFilter.set('');
    this.search.reset();
    this.applyFilter();
  }

  protected applyFilter(): void {
    this.filter.set(this.search.value.trim());
    this.cursors.set([firstCursor]);
  }

  protected page(rows?: OrgRow[]): void {
    const id = rows?.at(-1)?.id;
    if (id) { this.cursors.update(items => [...items, id]); }
    else if (!rows) { this.cursors.update(items => items.length > 1 ? items.slice(0, -1) : items); }
  }

  protected open(mode: Mode, row?: OrgRow): void {
    this.observation.set(null);
    this.form.reset();
    this.form.patchValue({
      name: row?.name ?? '', description: row?.description ?? '', directorateId: row?.directorateId ?? '',
      positionName: row?.positionName ?? '', inventoryNumber: row?.inventoryNumber ?? '',
      hourlyDowntimeCost: row?.hourlyDowntimeCost ?? null, responsibilityType: row?.responsibilityType ?? '',
    });
    this.form.controls.ownerId.setValue(row?.ownerId ?? '');
    this.write.notice.set('');
    this.editor.set({ mode, row });
  }

  protected checkedSlot(): Slot | null {
    const { directorateId, responsibilityType } = this.form.getRawValue();
    const value = this.observation();
    return value?.key === responsibilityType + '/' + directorateId ? value.slot : null;
  }

  protected inspectSlot(): void {
    const { directorateId, responsibilityType } = this.form.getRawValue();
    const type = this.types.find(value => value === responsibilityType);
    if (!type || (type !== 'CEO' && !directorateId) || this.checking()) { return; }
    this.checking.set(true);
    this.api.slot(type, directorateId).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.checking.set(false))).subscribe({
      next: slot => this.observation.set({ key: responsibilityType + '/' + directorateId, slot }),
      error: error => this.write.notice.set(errorMessage(error)),
    });
  }

  protected name(row: OrgRow): string {
    if (row.responsibilityType) {
      const person = this.options().people.find(entry => entry.id === row.personId);
      return this.roleLabels[row.responsibilityType] + ' · ' + (person?.fullName || person?.email || row.personId || text.unknown);
    }
    return row.name || row.fullName || row.email || row.id;
  }

  protected directorateName(id: string | null | undefined): string {
    return this.options().directorates.find(entry => entry.id === id)?.name ?? id ?? adminText.noDirectorate;
  }

  protected ownerName(id: string | undefined): string {
    const owner = this.options().people.find(person => person.id === id);
    return owner?.fullName || owner?.email || id || text.unknown;
  }

  protected save(): void {
    const editor = this.editor();
    if (!editor) { return; }
    const { mode, row } = editor;
    const kind = this.kind();
    const form = this.form.getRawValue();
    const base = directoryPath + kind;
    const path = row ? rowPath(base, row.id) : base;
    const version = row?.version ?? firstCursor;
    const ok = () => { this.editor.set(null); this.reload(); };
    const refresh = () => {
      // Конкурентную команду можно повторить только из обновлённой записи.
      if (row || kind === 'responsibilities') {
        this.editor.set(null);
      }
      this.reload();
    };
    if (['transfer', 'owner', 'state', 'replace'].includes(mode) && !form.confirmed) {
      this.write.notice.set(text.invalid); return;
    }
    if (mode === 'state' && row) {
      this.write.run(this.api.put(path + '/state', { version, isActive: !row.isActive, assignCeo: form.assignCeo }), ok, refresh);
      return;
    }
    if (mode === 'transfer' && row && form.directorateId && form.directorateId !== row.directorateId) {
      this.write.run(this.api.post(path + '/transfer', { version, directorateId: form.directorateId, positionName: form.positionName || null }), ok, refresh);
      return;
    }
    if (mode === 'owner' && form.ownerId && form.ownerId !== row?.ownerId) {
      this.write.run(this.api.put(path + '/owner', { version, ownerId: form.ownerId, confirmed: true }), ok, refresh);
      return;
    }
    if (mode !== 'edit' && mode !== 'create' && mode !== 'replace') { this.write.notice.set(text.invalid); return; }

    let body: object;
    const name = form.name.trim();
    const description = form.description.trim() || null;
    if (kind === 'responsibilities') {
      const slot = this.checkedSlot();
      if (!slot || (mode === 'replace' ? slot.assignmentId !== row?.id : !!slot.assignmentId)
        || (!form.personId && (form.responsibilityType !== 'CEO' || !form.userId))) {
        this.write.notice.set(slot?.assignmentId && mode === 'create' ? adminText.occupied : text.invalid); return;
      }
      body = { responsibilityType: form.responsibilityType,
        directorateId: form.responsibilityType === 'CEO' ? null : form.directorateId, personId: form.personId || null,
        userId: form.personId ? null : form.userId || null, positionName: form.positionName || null,
        previousId: row?.id ?? null, previousVersion: row?.version ?? null };
    } else if (kind === 'persons') {
      if (mode === 'create' && (!form.userId || !form.directorateId)) { this.write.notice.set(text.invalid); return; }
      body = mode === 'edit' ? { version, positionName: form.positionName || null } :
        { userId: form.userId, directorateId: form.directorateId, positionName: form.positionName || null };
    } else {
      if (!name) { this.write.notice.set(text.invalid); return; }
      if (kind === 'processes') {
        if (mode === 'create' && (!form.directorateId || !form.ownerId)) { this.write.notice.set(text.invalid); return; }
        body = mode === 'edit' ? { name, description, version } : { name, description, directorateId: form.directorateId, ownerId: form.ownerId };
      } else if (kind === 'assets') {
        if (form.hourlyDowntimeCost !== null && (!Number.isFinite(form.hourlyDowntimeCost) || form.hourlyDowntimeCost < 0)
          || (row && row.hourlyDowntimeCost !== form.hourlyDowntimeCost && !form.confirmed)) {
          this.write.notice.set(text.invalid); return;
        }
        body = { name, description, inventoryNumber: form.inventoryNumber.trim() || null, hourlyDowntimeCost: form.hourlyDowntimeCost };
      } else {
        body = { name };
      }
    }
    const request = mode === 'edit' ? this.api.put(path, body, kind === 'directorates' || kind === 'assets' ? { version } : undefined) :
      this.api.post(base, body);
    this.write.run(request, ok, refresh);
  }

  protected reload(): void {
    this.revision.update(value => value + 1);
  }
}
