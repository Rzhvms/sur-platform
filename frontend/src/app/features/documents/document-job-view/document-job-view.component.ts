import { JsonPipe } from '@angular/common';
import { Component, DestroyRef, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { EMPTY, catchError, distinctUntilChanged, exhaustMap, finalize, map, of, startWith, switchMap, takeWhile, timer } from 'rxjs';
import { money, utcTime } from '../../../shared/format';
import { statuses, text } from '../../../shared/text';
import { DataState, DataStateComponent } from '../../../shared/ui/data-state/data-state.component';
import { DocumentJob, DocumentKind, DocumentRow, JsonRecord, canPoll, documentPageSize, documentText, stateLabels } from '../document-contract';
import { csvFields, CsvField } from '../csv-preview';
import { DocumentsApiService, documentError } from '../documents-api.service';

const interval = 2500;
const loading = { kind: 'loading' } as const;

@Component({
  selector: 'sur-document-job',
  imports: [DataStateComponent, RouterLink, JsonPipe],
  templateUrl: './document-job-view.component.html',
  styleUrl: './document-job-view.component.scss',
})
export class DocumentJobViewComponent {
  readonly id = input.required<string>();
  readonly kind = input.required<DocumentKind>();
  readonly revision = input(0);
  readonly statusChanged = output<DocumentJob | null>();
  private readonly api = inject(DocumentsApiService);
  private readonly destroy = inject(DestroyRef);
  private readonly refresh = signal(0);
  protected readonly t = text;
  protected readonly w = documentText;
  protected readonly labels = stateLabels;
  protected readonly time = utcTime;
  protected readonly pageSize = documentPageSize;
  protected readonly cursors = signal([0]);
  protected readonly commandError = signal('');
  protected readonly sending = signal(false);
  private readonly query = computed(() => ({ id: this.id(), kind: this.kind(), revision: this.revision(), refresh: this.refresh() }));
  protected readonly state = toSignal(toObservable(this.query).pipe(switchMap(({ id, kind }) =>
    timer(0, interval).pipe(
      exhaustMap(() => this.api.status(id)),
      takeWhile(job => job.kind === kind && canPoll(job.state), true),
      map((job): DataState<DocumentJob> => {
        if (job.kind !== kind) {
          this.statusChanged.emit(null);
          return { kind: 'error', message: documentText.wrongJob };
        }
        this.statusChanged.emit(job);
        return { kind: 'ready', value: job };
      }),
      catchError(error => {
        this.statusChanged.emit(null);
        return of<DataState<DocumentJob>>({ kind: 'error', message: documentError(error) });
      }),
      startWith(loading),
    ),
  )), { initialValue: loading as DataState<DocumentJob> });

  private readonly rowQuery = computed(() => {
    const state = this.state();
    if (state.kind !== 'ready') { return null; }
    const job = state.value;
    const ready = job.kind === 'REPORT' ? job.capturedAt !== null : job.kind === 'IMPORT' &&
      (job.state === 'COMPLETED' || job.state === 'COMPLETED_WITH_ERRORS' || job.state === 'FAILED');
    return ready ? { id: job.id, after: this.cursors().at(-1) ?? 0 } : null;
  });
  protected readonly rows = toSignal(toObservable(this.rowQuery).pipe(
    distinctUntilChanged((a, b) => a?.id === b?.id && a?.after === b?.after),
    switchMap(query => query ? this.api.rows(query.id, query.after).pipe(
      map((value): DataState<DocumentRow[]> => ({ kind: 'ready', value })),
      catchError(error => of<DataState<DocumentRow[]>>({ kind: 'error', message: documentError(error) })),
      startWith(loading),
    ) : of(null)),
  ), { initialValue: null });

  protected reload(): void {
    this.refresh.update(value => value + 1);
  }

  protected next(rows: DocumentRow[]): void {
    const after = rows.at(-1)?.number;
    if (after) { this.cursors.update(items => [...items, after]); }
  }

  protected previous(): void {
    this.cursors.update(items => items.length > 1 ? items.slice(0, -1) : items);
  }

  protected retry(job: DocumentJob): void {
    if (!job.canRetry || this.sending()) { return; }
    this.commandError.set('');
    this.sending.set(true);
    this.api.retry(job.id).pipe(
      takeUntilDestroyed(this.destroy), finalize(() => this.sending.set(false)),
      catchError(error => { this.commandError.set(documentError(error, true)); this.reload(); return EMPTY; }),
    ).subscribe(() => { this.cursors.set([0]); this.reload(); });
  }

  protected download(job: DocumentJob): void {
    if (this.sending()) { return; }
    this.commandError.set('');
    this.sending.set(true);
    this.api.download(job).pipe(
      takeUntilDestroyed(this.destroy), finalize(() => this.sending.set(false)),
    ).subscribe({ error: error => this.commandError.set(documentError(error)) });
  }

  protected reportName(data: JsonRecord): string {
    const risk = asRecord(data['RiskDal']);
    return typeof risk?.['Name'] === 'string' ? risk['Name'] : text.unknown;
  }

  protected reportStatus(data: JsonRecord): string {
    const status = asRecord(data['RiskDal'])?.['Status'];
    return typeof status === 'string' ? statuses[status as keyof typeof statuses] ?? status : text.unknown;
  }

  protected reportStatusCode(data: JsonRecord): string {
    const status = asRecord(data['RiskDal'])?.['Status'];
    return typeof status === 'string' ? status : '';
  }

  protected nameValue(value: unknown): string {
    return typeof value === 'string' && value.length ? value : text.unknown;
  }

  protected reportProcesses(data: JsonRecord): string {
    const items = data['ProcessDal'];
    if (!Array.isArray(items)) { return text.unknown; }
    return items.map(item => asRecord(item)?.['ProcessDal'])
      .filter((name): name is string => typeof name === 'string' && name.length > 0).join(', ') || text.unknown;
  }

  protected reportLoss(data: JsonRecord): string {
    const items = data['RiskAssessmentDal'];
    const assessment = Array.isArray(items) ? items.map(asRecord).find(item => item?.['IsActive'] === true) : null;
    const value = assessment?.['LossAmount'];
    return typeof value === 'number' && Number.isFinite(value) ? money(value) : text.unknown;
  }

  protected code(value: unknown): string {
    return typeof value === 'string' && /^[A-Z][A-Z0-9_]{1,80}$/.test(value) ? value : text.unknown;
  }

  protected importReason(data: JsonRecord): string {
    const field = data['field'];
    if (typeof field === 'string' && field in csvFields) {
      return `${this.w.checkField} «${csvFields[field as CsvField]}»`;
    }
    switch (data['code']) {
      case 'RISK_RESOURCE_NOT_FOUND': return this.w.referenceMissing;
      case 'RISK_REFERENCE_INACTIVE': return this.w.referenceInactive;
      case 'ACCESS_DENIED': return this.w.importAccessDenied;
      case 'IMPORT_INVALID_ROW': return this.w.invalidRow;
      default: return '';
    }
  }
}

function asRecord(value: unknown): JsonRecord | null {
  return value !== null && typeof value === 'object' && !Array.isArray(value) ? value as JsonRecord : null;
}
