import { Component, DestroyRef, OnDestroy, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { api, errorMessage } from '../../../core/http/api';
import { calendarDate, utcTime } from '../../../shared/format';
import { text } from '../../../shared/text';
import { DataState, DataStateComponent } from '../../../shared/ui/data-state/data-state.component';
import { RiskSummary } from '../../risks/model';
import { RisksApiService } from '../../risks/risks-api.service';
import { AdminApiService, AdminWrite, Row, User, adminPageSize, adminText, rowPath } from '../admin-api.service';
import { ScheduleFields, schedulePayload } from '../review-contract';

type ReviewSource = Row & { responsibleUserId: string; externalSystemName?: string; timeZone?: string } &
  Partial<Omit<ScheduleFields, 'endDate'> & { endDate: string | null }>;
type Run = {
  id: string; occurredAt: string; scheduledDate: string | null; status: string; sourceType: string;
  sourceNameSnapshot: string | null; externalSystemNameSnapshot: string | null;
};
type Webhook = { settings: ReviewSource; riskIds: string[] };
type Receipt = { webhook: Webhook; token: string };
type Section = 'review-schedules' | 'review-webhooks' | 'review-runs';
const labels = {
  'review-schedules': 'Расписания', 'review-webhooks': 'Внешние источники', 'review-runs': 'Журнал запусков',
  responsible: 'Ответственный', external: 'Внешняя система', target: 'Выберите принятый риск', scope: 'Область запуска',
  start: 'Дата начала', recurrence: 'Периодичность', interval: 'Интервал', weekdays: 'Дни недели',
  until: 'Окончание', occurrences: 'Число запусков', endDate: 'Последняя дата', zone: 'Календарная зона',
  rotate: 'Заменить секрет', secret: 'Секрет источника', secretHint: 'Сохраните секрет сейчас. Он выдаётся только после создания или замены.',
  dismiss: 'Скрыть секрет', endpoint: 'Путь вебхука',
  confirmRotate: 'Прежний секрет перестанет действовать.', confirmRemove: 'Будущие запуски этой настройки прекратятся.',
} as const;
const recurrence = { NONE: 'Один раз', WEEKLY: 'По неделям', MONTHLY: 'По месяцам', YEARLY: 'По годам' };
const endings = { NEVER: 'Без ограничения', AFTER_OCCURRENCES: 'После заданного числа запусков', ON_DATE: 'По дату включительно' };
const weekdays = ['Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб', 'Вс'];
const sections: Section[] = ['review-schedules', 'review-webhooks', 'review-runs'];
const runStates: Record<string, string> = {
  PROCESSING: 'Выполняется', COMPLETED: 'Завершён', NO_ACTION: 'Без изменений',
  ERROR: 'Ошибка', COMPLETED_WITH_ERRORS: 'Завершён с ошибками', WAITING: 'Ожидает',
};
const sourceTypes: Record<string, string> = {
  REVIEW_SCHEDULE: labels['review-schedules'], REVIEW_WEBHOOK: labels['review-webhooks'],
  TRANSFER: 'Передача риска', RISK_CARD: 'Карточка риска', TREATMENT: 'Завершение меры',
};
const field = (value = '') => new FormControl(value, { nonNullable: true });
const defaultForm = { name: '', responsibleUserId: '', isActive: true, externalSystemName: '',
  startDate: '', recurrenceType: 'NONE', recurrenceInterval: 1, endType: 'NEVER', occurrenceCount: null, endDate: '' };

@Component({
  imports: [ReactiveFormsModule, RouterLink, DataStateComponent],
  templateUrl: './admin-reviews.component.html',
})
export class AdminReviewsComponent implements OnDestroy {
  private readonly api = inject(AdminApiService);
  private readonly risks = inject(RisksApiService);
  private readonly destroy = inject(DestroyRef);
  private readonly revision = signal(0);
  private editRevision = 0;
  protected readonly w = adminText;
  protected readonly t = text;
  protected readonly labels = labels;
  protected readonly sections = sections;
  protected readonly recurrence = recurrence;
  protected readonly recurrenceKeys = Object.keys(recurrence) as (keyof typeof recurrence)[];
  protected readonly endings = endings;
  protected readonly endingKeys = Object.keys(endings) as (keyof typeof endings)[];
  protected readonly weekdays = weekdays;
  protected readonly day = calendarDate;
  protected readonly time = utcTime;
  protected readonly statuses = runStates;
  protected readonly sourceTypes = sourceTypes;
  protected readonly size = adminPageSize;
  protected readonly section = signal<Section>('review-schedules');
  protected readonly offset = signal(0);
  protected readonly write = new AdminWrite(this.destroy);
  protected readonly selected = signal<ReviewSource | 'new' | null>(null);
  protected readonly confirmation = signal<{ action: 'rotate' | 'remove'; row: ReviewSource } | null>(null);
  protected readonly secret = signal<{ token: string; path: string } | null>(null);
  protected readonly selectedDays = signal<number[]>([]);
  protected readonly riskIds = signal<string[]>([]);
  protected readonly riskSearch = field();
  private readonly riskQuery = signal<string | null>(null);
  protected readonly riskResults = toSignal(toObservable(this.riskQuery).pipe(switchMap(search => {
    if (search === null) { return of([] as RiskSummary[]); }
    return this.risks.list({ status: 'ACCEPTED', search, sort: 'name', page: 1 }).pipe(
      catchError(error => { this.write.notice.set(errorMessage(error)); return of([]); }),
    );
  })), { initialValue: [] });
  protected readonly form = new FormGroup({
    name: field(), responsibleUserId: field(), isActive: new FormControl(true, { nonNullable: true }), externalSystemName: field(),
    startDate: field(), recurrenceType: field('NONE'), recurrenceInterval: new FormControl<number | null>(1),
    endType: field('NEVER'), occurrenceCount: new FormControl<number | null>(null), endDate: field(),
  });
  private readonly query = computed(() => ({ section: this.section(), offset: this.offset(), revision: this.revision() }));
  protected readonly data = toSignal(toObservable(this.query).pipe(switchMap(({ section, offset }) =>
    this.api.get<(ReviewSource | Run)[]>(section, { offset, limit: adminPageSize }).pipe(
      map((value): DataState<(ReviewSource | Run)[]> => ({ kind: 'ready', value })),
      catchError(error => of<DataState<(ReviewSource | Run)[]>>({ kind: 'error', message: errorMessage(error) })),
      startWith({ kind: 'loading' } as DataState<(ReviewSource | Run)[]>),
    ),
  )), { initialValue: { kind: 'loading' } as DataState<(ReviewSource | Run)[]> });
  protected readonly users = toSignal(this.api.options<User>('users', row => row.userId).pipe(
    map(rows => rows.filter(row => row.isActive)),
    catchError(error => { this.write.notice.set(errorMessage(error)); return of([]); }),
  ), { initialValue: [] });

  ngOnDestroy(): void {
    this.secret.set(null);
    this.form.reset();
    this.riskIds.set([]);
  }

  protected isRun(row: ReviewSource | Run): row is Run { return 'occurredAt' in row; }

  protected select(section: Section): void {
    this.close();
    this.section.set(section);
    this.offset.set(0);
    this.write.notice.set('');
  }

  protected close(): void {
    this.editRevision++;
    this.selected.set(null);
    this.confirmation.set(null);
    this.secret.set(null);
    this.riskIds.set([]);
    this.riskQuery.set(null);
    this.riskSearch.reset();
  }

  protected open(row?: ReviewSource): void {
    this.close();
    const revision = this.editRevision;
    if (row && this.section() === 'review-webhooks') {
      this.api.get<Webhook>(rowPath('review-webhooks', row.id)).pipe(takeUntilDestroyed(this.destroy)).subscribe({
        next: view => {
          if (this.section() === 'review-webhooks' && revision === this.editRevision) {
            this.fill(view.settings);
            this.riskIds.set(view.riskIds);
          }
        },
        error: error => this.write.notice.set(errorMessage(error)),
      });
    } else {
      this.fill(row);
    }
  }

  protected toggleDay(day: number): void {
    this.selectedDays.update(values => values.includes(day) ? values.filter(value => value !== day) : [...values, day].sort());
  }
  protected search(): void { this.riskQuery.set(this.riskSearch.value.trim()); }
  protected toggleRisk(id: string): void {
    this.riskIds.update(ids => ids.includes(id) ? ids.filter(value => value !== id) : [...ids, id]);
  }
  protected riskLabel(id: string): string { return this.riskResults().find(risk => risk.id === id)?.name ?? id; }

  protected save(): void {
    const source = this.selected();
    const section = this.section();
    const form = this.form.getRawValue();
    if (!source || section === 'review-runs' || !form.name.trim() || !form.responsibleUserId) {
      this.write.notice.set(text.invalid); return;
    }
    const row = source === 'new' ? null : source;
    const fields = { version: row?.version ?? null, name: form.name.trim(), responsibleUserId: form.responsibleUserId, isActive: form.isActive };
    const done = () => { this.close(); this.reload(); };
    if (section === 'review-schedules') {
      const schedule = schedulePayload({ ...form, weekdays: this.selectedDays() });
      if (!schedule) { this.write.notice.set(text.invalid); return; }
      const body = { ...fields, ...schedule };
      this.write.run(row ? this.api.put(rowPath(section, row.id), body) : this.api.post(section, body), done, done);
    } else {
      if (!form.externalSystemName.trim() || !this.riskIds().length) { this.write.notice.set(text.invalid); return; }
      const body = { ...fields, externalSystemName: form.externalSystemName.trim(), riskIds: this.riskIds() };
      if (row) {
        this.write.run(this.api.put(rowPath(section, row.id), body), done, done);
      } else {
        this.write.run(this.api.post<Receipt>(section, body), receipt => { done(); this.showSecret(receipt); }, done);
      }
    }
  }

  protected ask(action: 'rotate' | 'remove', row: ReviewSource): void {
    this.close();
    this.confirmation.set({ action, row });
  }

  protected confirm(): void {
    const change = this.confirmation();
    if (!change) { return; }
    const route = rowPath(this.section(), change.row.id);
    const done = () => { this.close(); this.reload(); };
    if (change.action === 'rotate') {
      this.write.run(this.api.post<Receipt>(route + '/rotate-token', { version: change.row.version, confirmed: true }),
        receipt => { done(); this.showSecret(receipt); }, done);
    } else {
      this.write.run(this.api.delete(route, change.row.version), done, done);
    }
  }

  protected page(step: number): void { this.offset.update(value => Math.max(0, value + step * adminPageSize)); }
  protected reload(): void { this.revision.update(value => value + 1); }

  private showSecret(receipt: Receipt): void {
    // Секрет живёт только в текущем компоненте и только из ответа создания или ротации.
    this.secret.set({ token: receipt.token, path: api + '/review-webhooks/' + encodeURIComponent(receipt.webhook.settings.id) });
  }

  private fill(row?: ReviewSource): void {
    this.form.reset({ ...defaultForm, ...row, endDate: row?.endDate ?? '', recurrenceInterval: row?.recurrenceInterval ?? 1 });
    this.selectedDays.set(row?.weekdays ?? []);
    this.selected.set(row ?? 'new');
  }
}
