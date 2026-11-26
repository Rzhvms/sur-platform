import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, finalize, forkJoin, map, of, startWith, switchMap, tap } from 'rxjs';
import { validTemporaryPassword } from './temporary-password';
import { errorMessage } from '../../core/http/api';
import { Session } from '../../core/auth/session';
import { text } from '../../shared/text';
import { DataState, DataStateView } from '../../shared/ui/data-state';
import { AdminApi, AdminWrite, OrgContext, ResponsibilityType, Role, Row, Slot, User, adminText, responsibilityLabels, rowPath } from './admin-api';

type Detail = { user: User | null; roles: Role[]; directorates: Row[] };
type Observation = { type: ResponsibilityType; directorateId: string; slot: Slot };
type Action = 'deactivate' | 'activate' | 'reissue';
const loading: DataState<Detail> = { kind: 'loading' };

@Component({
  imports: [ReactiveFormsModule, RouterLink, DataStateView],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './user-card.html',
})
export class AdminUserCard {
  private readonly api = inject(AdminApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  private readonly session = inject(Session);
  private readonly id = toSignal(this.route.paramMap.pipe(map(params => params.get('id'))), { requireSync: true });
  private readonly refresh = signal(0);
  protected readonly me = this.session.userId;
  protected readonly t = text;
  protected readonly w = adminText;
  protected readonly labels = responsibilityLabels;
  protected readonly types = Object.keys(responsibilityLabels) as ResponsibilityType[];
  protected readonly write = new AdminWrite(this.destroy);
  protected readonly relationships = signal<OrgContext | null>(null);
  protected readonly selectedAction = signal<Action | null>(null);
  protected readonly confirmed = new FormControl(false, { nonNullable: true });
  protected readonly loadingContext = signal(false);
  protected readonly checking = signal(false);
  protected readonly roleIds = signal<string[]>([]);
  protected readonly roleChoice = new FormControl<ResponsibilityType | ''>('', { nonNullable: true });
  protected readonly observations = signal<Observation[]>([]);
  protected readonly form = new FormGroup({
    fullName: new FormControl('', { nonNullable: true, validators: Validators.required }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    employee: new FormControl(false, { nonNullable: true }),
    directorateId: new FormControl('', { nonNullable: true }),
    positionName: new FormControl('', { nonNullable: true }),
    temporaryPassword: new FormControl('', { nonNullable: true }),
    repeatTemporaryPassword: new FormControl('', { nonNullable: true }),
  });
  private readonly query = computed(() => ({ id: this.id(), refresh: this.refresh() }));
  protected readonly data = toSignal(toObservable(this.query).pipe(switchMap(({ id }) => forkJoin({
    user: id ? this.api.get<User>(rowPath('users', id)) : of(null),
    roles: this.api.get<Role[]>('roles'),
    directorates: id ? of([] as Row[]) : this.api.options<Row>('organization/directorates', row => row.id, { active: true }),
  }).pipe(
    tap(({ user }) => this.populate(user)),
    map((value): DataState<Detail> => ({ kind: 'ready', value })),
    catchError((error: unknown) => of<DataState<Detail>>({ kind: 'error', message: errorMessage(error) })),
    startWith(loading),
  ))), { initialValue: loading });

  constructor() {
    this.form.controls.directorateId.valueChanges.pipe(takeUntilDestroyed(this.destroy)).subscribe(() => this.observations.set([]));
    this.form.controls.employee.valueChanges.pipe(takeUntilDestroyed(this.destroy)).subscribe(() => this.observations.set([]));
  }

  protected toggleRole(id: string): void {
    this.roleIds.update(ids => ids.includes(id) ? ids.filter(item => item !== id) : [...ids, id]);
  }

  protected removeObservation(type: ResponsibilityType): void {
    this.observations.update(items => items.filter(item => item.type !== type));
  }

  protected inspectSlot(): void {
    const type = this.roleChoice.value;
    const directorateId = type === 'CEO' ? '' : this.form.controls.directorateId.value;
    if (!type || (type !== 'CEO' && !directorateId) || this.checking()) { return; }
    this.checking.set(true);
    this.api.slot(type, directorateId).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.checking.set(false))).subscribe({
      next: slot => {
        const current = type === 'CEO' ? '' : this.form.controls.directorateId.value;
        if (this.form.controls.employee.value && current === directorateId) {
          this.observations.update(items => [...items.filter(item => item.type !== type), { type, directorateId, slot }]);
        }
      },
      error: error => this.write.notice.set(errorMessage(error)),
    });
  }

  protected submit(detail: Detail): void {
    const { user, roles } = detail;
    const form = this.form.getRawValue();
    const email = form.email.trim();
    const fullName = form.fullName.trim();
    const roleIds = this.roleIds();
    const rolesChanged = !!user && (roleIds.length !== user.roleIds.length || roleIds.some(id => !user.roleIds.includes(id)));
    if (!user?.isActive && (!email || this.form.controls.email.invalid)) { this.write.notice.set(text.invalid); return; }
    if ((!user || user.isActive) && (!fullName || ((!user || rolesChanged) &&
      (!roleIds.length || roleIds.some(id => !roles.some(role => role.id === id && role.isActive)))))) {
      this.write.notice.set(fullName ? adminText.activeRole : text.invalid);
      return;
    }
    if (user) {
      const { version } = user;
      const body = user.isActive ? { version, fullName, ...(rolesChanged ? { roleIds } : {}) } : { version, email };
      this.write.run(this.api.patch<User>(rowPath('users', user.userId), body), saved => this.populate(saved, true), () => this.reload());
      return;
    }
    if (!this.passwordValid()) { this.write.notice.set(adminText.temporaryPasswordInvalid); return; }
    const checked = this.observations();
    if (form.employee && (checked.some(item => item.slot.assignmentId) || (!form.directorateId && !checked.some(item => item.type === 'CEO')))) {
      this.write.notice.set(checked.some(item => item.slot.assignmentId) ? adminText.occupied : text.invalid);
      return;
    }
    const organization = form.employee ? {
      directorateId: form.directorateId || null, positionName: form.positionName.trim() || null,
      responsibilities: checked.map(({ type, directorateId, slot }) => ({ responsibilityType: type, directorateId: directorateId || null, observedAt: slot.observedAt })),
    } : null;
    this.write.run(this.api.post<User>('users', { email, fullName, roleIds, organization, temporaryPassword: form.temporaryPassword }).pipe(finalize(() => this.clearPassword())), saved =>
      void this.router.navigate(['/admin/users', saved.userId], { replaceUrl: true }), () => {
      // Повторно наблюдаем проверенные позиции. Данные мастера и выборы остаются в форме.
      if (!checked.length) { return; }
      this.observations.set([]);
      this.checking.set(true);
      forkJoin(checked.map(item => this.api.slot(item.type, item.directorateId).pipe(map(slot => ({ ...item, slot })))))
        .pipe(takeUntilDestroyed(this.destroy), finalize(() => this.checking.set(false))).subscribe({
          next: current => {
            if (this.form.controls.employee.value) {
              this.observations.set(current.filter(item => !item.directorateId || item.directorateId === this.form.controls.directorateId.value));
            }
          },
          error: () => {},
        });
    });
  }

  protected prepare(action: Action, user: User): void {
    this.clearPassword();
    this.selectedAction.set(action);
    this.confirmed.reset();
    this.relationships.set(null);
    this.write.notice.set('');
    if (action !== 'deactivate') { return; }
    this.loadingContext.set(true);
    this.api.get<OrgContext>(rowPath('users', user.userId) + '/organization').pipe(
      takeUntilDestroyed(this.destroy), finalize(() => this.loadingContext.set(false)),
    ).subscribe({ next: value => this.relationships.set(value), error: error => this.write.notice.set(errorMessage(error)) });
  }

  protected command(user: User, action: Action): void {
    if (!this.confirmed.value || (action === 'deactivate' && (!this.relationships() || this.blocked()))) { return; }
    const path = rowPath('users', user.userId);
    const version = user.version;
    const data = this.data();
    const activeRoles = data.kind === 'ready' ? data.value.roles.filter(role => role.isActive && this.roleIds().includes(role.id)) : [];
    if (action === 'activate' && !activeRoles.length) { this.write.notice.set(adminText.activeRole); return; }
    if (action !== 'deactivate' && !this.passwordValid()) { this.write.notice.set(adminText.temporaryPasswordInvalid); return; }
    const temporaryPassword = this.form.controls.temporaryPassword.value;
    const request = action === 'reissue' ? this.api.post<User>(path + '/reissue-access', { version, temporaryPassword })
      : this.api.post<User>(path + '/' + action, action === 'activate' ? { version, temporaryPassword, roleIds: activeRoles.map(role => role.id) } : { version });
    this.write.run(request.pipe(finalize(() => this.clearPassword())), saved => {
      this.populate(saved, true);
      this.selectedAction.set(null);
    }, () => {
      this.reload(false);
      if (action === 'deactivate') {
        const notice = this.write.notice();
        this.prepare(action, user);
        this.write.notice.set(notice);
      }
    });
  }

  protected blocked(): boolean {
    const links = this.relationships();
    return !!(links?.processes.length || links?.responsibilities.length || links?.treatments.length);
  }

  protected cancelAction(): void {
    this.selectedAction.set(null);
    this.clearPassword();
  }

  private passwordValid(): boolean {
    const { temporaryPassword, repeatTemporaryPassword } = this.form.getRawValue();
    return validTemporaryPassword(temporaryPassword, repeatTemporaryPassword);
  }

  private clearPassword(): void {
    this.form.controls.temporaryPassword.reset();
    this.form.controls.repeatTemporaryPassword.reset();
  }

  protected reload(clear = true): void {
    if (clear) { this.selectedAction.set(null); }
    this.refresh.update(value => value + 1);
  }

  private populate(user: User | null, reread = false): void {
    this.clearPassword();
    if (reread) { this.reload(); }
    this.form.patchValue({ fullName: user?.fullName ?? '', email: user?.email ?? '' });
    this.roleIds.set(user?.roleIds ?? []);
  }
}
