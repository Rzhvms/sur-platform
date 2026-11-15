import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { Observable, Subject, catchError, combineLatest, debounceTime, exhaustMap, forkJoin, map, of, startWith, switchMap, timer } from 'rxjs';
import { Session } from '../../core/auth/session';
import { errorMessage } from '../../core/http/api';
import { utcTime } from '../../shared/format';
import { statuses, text } from '../../shared/text';
import { Confirm } from '../../shared/ui/confirm';
import { DataState, DataStateView } from '../../shared/ui/data-state';
import { Rating, Status } from '../../shared/ui/status';
import { TableFrame } from '../../shared/ui/table-frame';
import { boardExample, listExample } from './demo';
import { hasNextColumnPage } from './board-page';
import { Dimension, RiskColumn, RiskOption, RiskQuery, RiskSummary, maxPage, pageSize, apiQuery, guid, readQuery, riskStatuses, urlQuery } from './model';
import { RisksApi } from './risks-api';

interface CollectionData { rows: RiskSummary[]; columns: RiskColumn[] }
type ExampleState = 'data' | 'loading' | 'empty' | 'error';
const load: DataState<CollectionData> = { kind: 'loading' };

@Component({
  selector: 'sur-risk-collection',
  imports: [ReactiveFormsModule, RouterLink, RouterLinkActive, Confirm, DataStateView, Rating, Status, TableFrame],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './collection.html',
})
export class RiskCollection {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly risks = inject(RisksApi);
  private readonly session = inject(Session);
  private boardPage?: { key: string; columns: RiskColumn[] };
  protected readonly canCreate = computed(() => !this.demo && this.session.hasRight('create_risk'));
  protected readonly moreInColumn = hasNextColumnPage;
  protected readonly filterOptions = signal<Partial<Record<Dimension, RiskOption[]>>>({});
  protected readonly filters = [
    { key: 'directorateId', label: text.directorate }, { key: 'processId', label: text.process },
    { key: 'ownerId', label: text.owner }, { key: 'assetId', label: text.asset }, { key: 'categoryId', label: text.category },
  ] as const;
  private readonly destroyRef = inject(DestroyRef);
  private readonly reload = new Subject<void>();
  private readonly confirm = viewChild.required(Confirm);
  protected readonly t = text;
  protected readonly labels = statuses;
  protected readonly guidPattern = guid.source.replaceAll('a-f', 'a-fA-F');
  protected readonly statuses = riskStatuses;
  protected readonly utcTime = utcTime;
  protected readonly demo = this.route.parent?.snapshot.data['demo'] === true;
  protected readonly isBoard = this.route.snapshot.data['board'] === true;
  protected readonly root = this.demo ? '/demo/risks' : '/risks';
  protected readonly query = toSignal(this.route.queryParamMap.pipe(map(readQuery)), { requireSync: true });
  protected readonly example = signal<ExampleState>('data');
  protected readonly filterLink = computed(() => urlQuery({ ...this.query(), page: 1, offsets: undefined }));
  protected readonly detailLink = computed(() => ({ ...urlQuery(this.query()), from: this.isBoard ? 'board' : null }));
  protected readonly form = inject(FormBuilder).nonNullable.group({
    search: this.query().search, status: this.query().status, sort: this.query().sort,
    directorateId: this.query().directorateId ?? '', processId: this.query().processId ?? '',
    ownerId: this.query().ownerId ?? '', assetId: this.query().assetId ?? '', categoryId: this.query().categoryId ?? '',
  });
  protected readonly state = toSignal(combineLatest([this.route.queryParamMap.pipe(map(readQuery)), this.reload.pipe(startWith(undefined))]).pipe(
    switchMap(([query]) => this.fetch(query)),
  ), { initialValue: load });

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const { search, status, sort, ...query } = readQuery(params);
      // Keep unsubmitted whitespace while the URL reflects the normalized search value.
      const sameSearch = this.form.controls.search.value.trim() === search;
      this.form.patchValue({
        ...(sameSearch ? {} : { search }), status, sort,
        directorateId: query.directorateId ?? '', processId: query.processId ?? '',
        ownerId: query.ownerId ?? '', assetId: query.assetId ?? '', categoryId: query.categoryId ?? '',
      }, { emitEvent: false });
    });
    this.form.controls.search.valueChanges.pipe(
      debounceTime(270), takeUntilDestroyed(this.destroyRef),
    ).subscribe(() => this.applyFilters());
  }

  protected applyFilters(): void {
    const value = this.form.getRawValue();
    const search = value.search.trim().slice(0, 250);
    const current = this.query();
    const changed = current.search !== search || current.status !== value.status || current.sort !== value.sort
      || this.filters.some(({ key }) => (current[key] ?? '') !== value[key].trim());
    if (changed && this.filters.every(({ key }) => !value[key].trim() || guid.test(value[key].trim()))) {
      this.navigate({ ...value, search, page: 1 });
    }
  }

  protected changePage(change: number): void {
    this.navigate({ ...this.query(), page: this.query().page + change });
  }

  protected nextPage(data: CollectionData): boolean {
    if (this.query().page >= maxPage) {
      return false;
    }
    return data.rows.length === pageSize;
  }

  protected columnIsLoading(column: RiskColumn): boolean {
    const query = this.query();
    return (!query.status || column.status === query.status) && column.offset !== (query.offsets?.[column.status] ?? 0);
  }

  protected changeColumn(column: RiskColumn, direction: number): void {
    const offset = column.offset + direction * pageSize;
    this.navigate({ ...this.query(), page: 1, offsets: { ...this.query().offsets, [column.status]: offset } });
  }

  protected isEmptyBoard(columns: RiskColumn[]): boolean {
    return columns.every(column => column.total === 0);
  }

  protected hasFilters(): boolean {
    const query = this.query();
    return !!(query.search || query.status || this.filters.some(({ key }) => query[key]));
  }

  protected showClear(opener: HTMLButtonElement): void {
    this.confirm().show(opener);
  }

  protected clear(): void {
    this.form.setValue({ search: '', status: '', sort: 'recent', directorateId: '', processId: '',
      ownerId: '', assetId: '', categoryId: '' }, { emitEvent: false });
    this.navigate({ search: '', status: '', sort: 'recent', page: 1 });
  }

  protected retry(): void {
    this.example.set('data');
    this.boardPage = undefined;
    this.reload.next();
  }

  protected selectExample(event: Event): void {
    if (!(event.target instanceof HTMLSelectElement)) {
      return;
    }
    const value = event.target.value;
    if (value === 'data' || value === 'loading' || value === 'empty' || value === 'error') {
      this.example.set(value);
    }
  }

  private navigate(query: RiskQuery): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: urlQuery(query) });
  }

  private fetch(query: RiskQuery): Observable<DataState<CollectionData>> {
    if (this.demo) {
      return of({ kind: 'ready', value: this.isBoard
        ? { rows: [], columns: boardExample(query) } : { rows: listExample(query), columns: [] } });
    }

    // Route changes cancel the previous lookup; each board poll waits for the previous one.
    const request = this.isBoard
      ? timer(0, 30_000).pipe(exhaustMap(tick => this.loadBoard(query, tick > 0).pipe(
        map(columns => this.ready({ columns, rows: [] })),
        catchError((error: unknown) => of(this.failed(error))),
      )))
      : this.risks.list(query).pipe(
        map(rows => this.ready({ rows, columns: [] })),
        catchError((error: unknown) => of(this.failed(error))),
      );
    const cached = this.isBoard && this.boardPage?.key === this.boardKey(query) ? this.boardPage.columns : null;
    return request.pipe(startWith(cached ? this.ready({ rows: [], columns: cached }) : load));
  }

  /** Переход по колонке запрашивает только её. При смене фильтра заново читаем весь стартовый срез. */
  private loadBoard(query: RiskQuery, poll: boolean): Observable<RiskColumn[]> {
    const offsets = query.offsets;
    const key = this.boardKey(query);
    const saved = this.boardPage?.key === key && !poll ? this.boardPage.columns : null;
    return (saved ? of(saved) : this.risks.board(query)).pipe(
      switchMap(columns => !columns.length ? of([]) : forkJoin(columns.map(column => {
        const offset = !query.status || column.status === query.status ? offsets?.[column.status] ?? 0 : 0;
        return column.offset === offset ? of(column) : this.risks.boardColumn(query, column.status, offset);
      }))),
      map(columns => {
        this.boardPage = { key, columns };
        return columns;
      }),
    );
  }

  private boardKey(query: RiskQuery): string {
    return JSON.stringify(apiQuery({ ...query, page: 1 }));
  }

  private rememberOptions(rows: RiskSummary[]): void {
    const options = this.filterOptions();
    const next = { ...options };
    const remember = (key: Dimension, id: string | null, name: string | null) => {
      if (id && name && !next[key]?.some(item => item.id === id)) {
        next[key] = [...(next[key] ?? []), { id, name }];
      }
    };
    for (const row of rows) {
      remember('categoryId', row.categoryId, row.categoryName);
      remember('assetId', row.assetId, row.assetName);
      for (const process of row.processes) {
        remember('directorateId', process.directorateId, process.directorateName);
        remember('processId', process.processId, process.processName);
        remember('ownerId', process.ownerId, process.ownerName);
      }
    }
    this.filterOptions.set(next);
  }

  private ready(value: CollectionData): DataState<CollectionData> {
    this.rememberOptions([...value.rows, ...value.columns.flatMap(column => column.items)]);
    return { kind: 'ready', value };
  }

  private failed(error: unknown): DataState<CollectionData> {
    return { kind: 'error', message: errorMessage(error) };
  }
}
