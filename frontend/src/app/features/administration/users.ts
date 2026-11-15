import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { errorMessage } from '../../core/http/api';
import { text } from '../../shared/text';
import { DataState, DataStateView } from '../../shared/ui/data-state';
import { AdminApi, adminPageSize, adminText, firstCursor } from './admin-api';

type LegacyUser = { userId: string; email: string; firstName?: string; lastName?: string; patronymic?: string; fullName?: string; isActive?: boolean };
const loading: DataState<LegacyUser[]> = { kind: 'loading' };

@Component({
  imports: [RouterLink, DataStateView],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="panel-heading"><h1>{{ w.users }}</h1><a class="action-link" routerLink="new">{{ w.create }}</a></div>
    @let data = result();
    @if (data.kind === 'loading') { <sur-data-state kind="loading" /> }
    @else if (data.kind === 'error') { <sur-data-state kind="error" [message]="data.message" (retry)="refresh()" /> }
    @else {
      <section class="panel">
        <ul class="related-list">
          @for (row of data.value; track row.userId) {
            <li><a [routerLink]="[row.userId]">{{ name(row) }}</a><p class="muted">
              @if (row.email && row.email !== name(row)) { {{ row.email }} }
              @if (row.isActive !== undefined) { · {{ row.isActive ? w.active : w.inactive }} }
            </p></li>
          } @empty { <li>{{ w.usersEmpty }}</li> }
        </ul>
        <div class="pagination">
          <button type="button" [disabled]="cursors().length === 1" (click)="previous()">{{ t.previous }}</button>
          <button type="button" [disabled]="data.value.length < size" (click)="next(data.value)">{{ t.next }}</button>
        </div>
      </section>
    }
  `,
})
export class AdminUsers {
  private readonly api = inject(AdminApi);
  private readonly revision = signal(0);
  protected readonly cursors = signal([firstCursor]);
  protected readonly t = text;
  protected readonly w = adminText;
  protected readonly size = adminPageSize;
  private readonly query = computed(() => ({ after: this.cursors().at(-1) ?? firstCursor, revision: this.revision() }));
  protected readonly result = toSignal(toObservable(this.query).pipe(switchMap(({ after }) =>
    this.api.get<{ userList: LegacyUser[] }>('users/list', { after, limit: adminPageSize }).pipe(
      map(({ userList }): DataState<LegacyUser[]> => ({ kind: 'ready', value: userList })),
      catchError(error => of<DataState<LegacyUser[]>>({ kind: 'error', message: errorMessage(error) })),
      startWith(loading),
    ),
  )), { initialValue: loading });

  protected name(row: LegacyUser): string {
    return row.fullName || [row.lastName, row.firstName, row.patronymic].filter(Boolean).join(' ') || row.email || row.userId;
  }
  protected previous(): void { this.cursors.update(values => values.length > 1 ? values.slice(0, -1) : values); }
  protected next(rows: LegacyUser[]): void {
    const cursor = rows.at(-1)?.userId;
    if (cursor) { this.cursors.update(values => [...values, cursor]); }
  }
  protected refresh(): void { this.revision.update(value => value + 1); }
}
