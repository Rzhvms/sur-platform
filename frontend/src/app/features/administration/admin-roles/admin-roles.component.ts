import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, forkJoin, map, of, startWith, switchMap } from 'rxjs';
import { errorMessage } from '../../../core/http/api';
import { text } from '../../../shared/text';
import { DataState, DataStateComponent } from '../../../shared/ui/data-state/data-state.component';
import { AdminApiService, AdminWrite, Right, Role, adminText, rowPath } from '../admin-api.service';

type Catalog = { roles: Role[]; rights: Right[] };
const loading: DataState<Catalog> = { kind: 'loading' };

@Component({
  imports: [DataStateComponent],
  templateUrl: './admin-roles.component.html',
})
export class AdminRolesComponent {
  private readonly api = inject(AdminApiService);
  private readonly revision = signal(0);
  private readonly id = signal('');
  protected readonly w = adminText;
  protected readonly t = text;
  protected readonly write = new AdminWrite(inject(DestroyRef));
  protected readonly active = signal(false);
  protected readonly rights = signal<string[]>([]);
  protected readonly data = toSignal(toObservable(this.revision).pipe(switchMap(() => forkJoin({
    roles: this.api.get<Role[]>('roles'), rights: this.api.get<Right[]>('roles/rights'),
  }).pipe(
    map((value): DataState<Catalog> => {
      const selected = value.roles.find(role => role.id === this.id()) ?? value.roles[0];
      if (selected) { this.select(selected); }
      return { kind: 'ready', value };
    }),
    catchError(error => of<DataState<Catalog>>({ kind: 'error', message: errorMessage(error) })),
    startWith(loading),
  ))), { initialValue: loading });
  protected readonly selected = computed(() => {
    const data = this.data();
    return data.kind === 'ready' ? data.value.roles.find(role => role.id === this.id()) : undefined;
  });

  protected select(role: Role): void {
    this.id.set(role.id);
    this.active.set(role.isActive);
    this.rights.set(role.rightIds);
  }

  protected toggle(id: string): void {
    this.rights.update(ids => ids.includes(id) ? ids.filter(item => item !== id) : [...ids, id]);
  }

  protected save(role: Role): void {
    this.write.run(this.api.put<Role>(rowPath('roles', role.id),
      { version: role.version, isActive: this.active(), rightIds: this.rights() }), () => this.refresh(), () => this.refresh());
  }

  protected refresh(): void {
    this.revision.update(value => value + 1);
  }
}
