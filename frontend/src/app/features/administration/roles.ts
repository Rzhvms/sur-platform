import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, forkJoin, map, of, startWith, switchMap } from 'rxjs';
import { errorMessage } from '../../core/http/api';
import { text } from '../../shared/text';
import { DataState, DataStateView } from '../../shared/ui/data-state';
import { AdminApi, AdminWrite, Right, Role, adminText, rowPath } from './admin-api';

type Catalog = { roles: Role[]; rights: Right[] };
const loading: DataState<Catalog> = { kind: 'loading' };

@Component({
  imports: [DataStateView],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>{{ w.roles }}</h1>
    @if (write.notice()) { <p class="notice" role="status">{{ write.notice() }}</p> }
    @if (write.dependencyIds().length) { <p>{{ w.blockers }}: {{ write.dependencyIds().join(', ') }}</p> }
    @let state = data();
    @if (state.kind === 'loading') { <sur-data-state kind="loading" /> }
    @else if (state.kind === 'error') { <sur-data-state kind="error" [message]="state.message" (retry)="refresh()" /> }
    @else {
      <nav class="action-row" [attr.aria-label]="w.roles">
        @for (entry of state.value.roles; track entry.id) {
          <button type="button" [disabled]="write.busy()" [attr.aria-pressed]="selected()?.id === entry.id"
            (click)="select(entry)">{{ entry.name || entry.code }} @if (!entry.isActive) { ({{ w.inactive }}) }</button>
        }
      </nav>
      @if (selected(); as role) {
        <section class="panel">
          <h2>{{ role.name || role.code }}</h2>
          @if (role.description) { <p>{{ role.description }}</p> }
          <p class="muted">{{ w.version }} · <code>{{ role.version }}</code></p>
          <fieldset class="form-stack" [disabled]="write.busy()">
            <legend>{{ w.rights }}</legend>
            <label class="inline-check"><input type="checkbox" [checked]="active()" [disabled]="role.code === 'ADMIN'"
              (change)="active.set(!active())" /> {{ w.active }}</label>
            @for (right of state.value.rights; track right.id) {
              <label class="inline-check">
                <input type="checkbox" [checked]="rights().includes(right.id)" [disabled]="right.code === 'admin' || role.code === 'ADMIN'" (change)="toggle(right.id)" />
                <span>{{ right.name || right.code }} @if (right.name && right.code) { <code class="muted">{{ right.code }}</code> }</span>
              </label>
            }
            @if (role.code !== 'ADMIN') { <button class="primary" type="button" (click)="save(role)">{{ t.save }}</button> }
          </fieldset>
        </section>
      }
    }
  `,
})
export class AdminRoles {
  private readonly api = inject(AdminApi);
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
