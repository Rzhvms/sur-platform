import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { errorMessage } from '../../../core/http/api';
import { text } from '../../../shared/text';
import { DataState, DataStateComponent } from '../../../shared/ui/data-state/data-state.component';
import { AdminApiService, adminPageSize, adminText, firstCursor } from '../admin-api.service';

type LegacyUser = { userId: string; email: string; firstName?: string; lastName?: string; patronymic?: string; fullName?: string; isActive?: boolean };
const loading: DataState<LegacyUser[]> = { kind: 'loading' };

@Component({
  imports: [RouterLink, DataStateComponent],
  templateUrl: './admin-users.component.html',
})
export class AdminUsersComponent {
  private readonly api = inject(AdminApiService);
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
