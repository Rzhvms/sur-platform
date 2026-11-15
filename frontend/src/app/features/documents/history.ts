import { JsonPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { utcTime } from '../../shared/format';
import { text } from '../../shared/text';
import { DataState, DataStateView } from '../../shared/ui/data-state';
import { DatePeriod, JsonRecord, calendarDay, documentPageSize, documentText } from './document-contract';
import { DocumentJobView } from './document-job';
import { DocumentRequest } from './document-request';
import { DocumentsApi, documentError } from './documents-api';

@Component({
  imports: [ReactiveFormsModule, DocumentJobView, DataStateView, JsonPipe],
  templateUrl: './history.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminHistory {
  private readonly api = inject(DocumentsApi);
  private readonly revision = signal(0);
  protected readonly request = new DocumentRequest<{ period: DatePeriod }>();
  protected readonly w = documentText;
  protected readonly t = text;
  protected readonly pageSize = documentPageSize;
  protected readonly offset = signal(0);
  protected readonly period = signal<DatePeriod | null>(null);
  protected readonly form = new FormGroup({
    from: new FormControl('', { nonNullable: true }),
    to: new FormControl('', { nonNullable: true }),
  });
  private readonly query = computed(() => ({ period: this.period(), offset: this.offset(), revision: this.revision() }));
  protected readonly events = toSignal(toObservable(this.query).pipe(switchMap(({ period, offset }) => period
    ? this.api.events(period, offset).pipe(
        map((value): DataState<JsonRecord[]> => ({ kind: 'ready', value })),
        catchError(error => of<DataState<JsonRecord[]>>({ kind: 'error', message: documentError(error) })),
        startWith({ kind: 'loading' } as const),
      ) : of(null))), { initialValue: null });

  protected view(): void {
    const period = this.selectedPeriod();
    if (!period) { return; }
    this.period.set(period);
    this.offset.set(0);
  }

  protected export(): void {
    const period = this.request.attempt()?.period ?? this.selectedPeriod();
    if (period) {
      this.request.send({ period }, body => this.api.history(body));
    }
  }

  protected turnPage(step: number): void {
    this.offset.update(offset => Math.max(0, offset + step * documentPageSize));
  }

  protected reload(): void {
    this.revision.update(value => value + 1);
  }

  protected time(event: JsonRecord): string {
    return typeof event['occurredAt'] === 'string' ? utcTime(event['occurredAt']) : text.unknown;
  }

  private selectedPeriod(): DatePeriod | null {
    const period = this.form.getRawValue();
    if (!calendarDay(period.from) || !calendarDay(period.to) || period.from === '0001-01-01' ||
      period.to < period.from || period.to === '9999-12-31') {
      this.request.error.set(documentText.invalidPeriod);
      return null;
    }
    this.request.error.set('');
    return period;
  }
}
